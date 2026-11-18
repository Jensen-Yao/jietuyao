using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using STranslate.Plugin.Ocr.PpOnnx.ViewModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.PpOnnx;

/// <summary>
/// 本地 PP-OCR（PaddleOCR ONNX）插件：det(DB 后处理简化) + rec(CTC 贪心)，
/// 识别字典内嵌于 rec 模型 metadata(character)，完全离线。
/// </summary>
public class Main : IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;
    private PpOcrEngine? _engine;

    public IEnumerable<LangEnum> SupportedLanguages =>
    [
        LangEnum.Auto,
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
        LangEnum.Japanese,
        LangEnum.Korean,
    ];

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
        _viewModel?.Dispose();
    }

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        var result = new OcrResult();
        if (string.IsNullOrWhiteSpace(Settings.DetModelPath) || !File.Exists(Settings.DetModelPath))
        {
            return result.Fail("det 模型不存在：" + Settings.DetModelPath);
        }
        if (string.IsNullOrWhiteSpace(Settings.RecModelPath) || !File.Exists(Settings.RecModelPath))
        {
            return result.Fail("rec 模型不存在：" + Settings.RecModelPath);
        }

        try
        {
            var engine = GetEngine();
            if (engine == null)
                return result.Fail("引擎初始化失败，请检查模型文件");

            using var bitmap = DecodeImage(request.ImageData);
            if (bitmap == null)
                return result.Fail("图片解码失败");

            var contents = await Task.Run(() => engine.Recognize(bitmap, cancellationToken), cancellationToken);
            foreach (var (text, points) in contents)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var content = new OcrContent { Text = text };
                foreach (var (x, y) in points)
                    content.BoxPoints.Add(new BoxPoint(x, y));
                result.OcrContents.Add(content);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return result.Fail($"PP-OCR 识别失败：{ex.Message}");
        }
    }

    private PpOcrEngine? GetEngine()
    {
        if (_engine is { DetPath: var dp, RecPath: var rp } && dp == Settings.DetModelPath && rp == Settings.RecModelPath)
            return _engine;
        _engine?.Dispose();
        try
        {
            _engine = new PpOcrEngine(Settings.DetModelPath, Settings.RecModelPath, Settings.Threads);
        }
        catch
        {
            _engine = null;
        }
        return _engine;
    }

    internal static Bitmap? DecodeImage(byte[] data)
    {
        using var ms = new MemoryStream(data);
        try
        {
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// PP-OCR 推理引擎（det + rec），会话级线程安全
/// </summary>
public sealed class PpOcrEngine : IDisposable
{
    private const float DetThreshold = 0.3f;
    private const int DetMaxSide = 960;
    private const int RecHeight = 48;
    private const int RecMaxWidth = 960;

    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    private readonly InferenceSession _det;
    private readonly InferenceSession _rec;
    private readonly string _detInput;
    private readonly string _recInput;
    private readonly string[] _vocab; // index 0 = CTC blank
    private readonly object _inferLock = new();

    public string DetPath { get; }
    public string RecPath { get; }

    public PpOcrEngine(string detPath, string recPath, int threads)
    {
        DetPath = detPath;
        RecPath = recPath;
        var options = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Clamp(threads, 1, 8),
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        _det = new InferenceSession(detPath, options);
        _rec = new InferenceSession(recPath, options);
        _detInput = _det.InputMetadata.Keys.First();
        _recInput = _rec.InputMetadata.Keys.First();

        // 字典内嵌于 rec 模型 metadata（与 v1 ppocr_rust 同源约定）
        var charset = _rec.ModelMetadata.CustomMetadataMap.TryGetValue("character", out var c) ? c : string.Empty;
        var vocab = new List<string> { string.Empty }; // 0 = blank
        vocab.AddRange(charset.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        _vocab = [.. vocab];
    }

    /// <summary>识别：返回每行文本与四角坐标（原图像素）</summary>
    public List<(string Text, (float X, float Y)[] Points)> Recognize(Bitmap image, CancellationToken cancellationToken)
    {
        lock (_inferLock)
        {
            var boxes = DetectBoxes(image);
            var results = new List<(string, (float, float)[])>();
            foreach (var box in boxes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = RecognizeCrop(image, box);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var (x, y, w, h) = box;
                var points = new (float, float)[]
                {
                    (x, y), (x + w, y), (x + w, y + h), (x, y + h),
                };
                results.Add((text, points));
            }
            return results;
        }
    }

    // ===== 检测 =====
    private List<(float X, float Y, float W, float H)> DetectBoxes(Bitmap image)
    {
        // 缩放
        var scale = Math.Min(1.0f, (float)DetMaxSide / Math.Max(image.Width, image.Height));
        var targetW = Math.Max(32, (int)Math.Round(image.Width * scale / 32) * 32);
        var targetH = Math.Max(32, (int)Math.Round(image.Height * scale / 32) * 32);

        using var resized = new Bitmap(image, targetW, targetH);
        var input = BuildTensor(resized, targetW, targetH);

        float[] probMap;
        int mapW, mapH;
        using (var results = _det.Run([NamedOnnxValue.CreateFromTensor(_detInput, input)]))
        {
            var output = (DenseTensor<float>)results.First().AsTensor<float>();
            mapH = output.Dimensions[2];
            mapW = output.Dimensions[3];
            probMap = output.Buffer.ToArray();
        }

        // 阈值二值化 + 连通域（BFS）→ 轴对齐框
        var binary = new bool[probMap.Length];
        for (var i = 0; i < probMap.Length; i++)
            binary[i] = probMap[i] > DetThreshold;

        var visited = new bool[mapW * mapH];
        var boxes = new List<(float X, float Y, float W, float H)>();
        var queue = new Queue<int>();
        for (var startY = 0; startY < mapH; startY++)
        {
            for (var startX = 0; startX < mapW; startX++)
            {
                var start = startY * mapW + startX;
                if (visited[start] || !binary[start])
                    continue;

                queue.Clear();
                queue.Enqueue(start);
                visited[start] = true;
                var minX = startX; var maxX = startX; var minY = startY; var maxY = startY;

                while (queue.Count > 0)
                {
                    var idx = queue.Dequeue();
                    var x = idx % mapW;
                    var y = idx / mapW;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);

                    foreach (var (dx, dy) in NeighborOffsets)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= mapW || ny >= mapH)
                            continue;
                        var n = ny * mapW + nx;
                        if (!visited[n] && binary[n])
                        {
                            visited[n] = true;
                            queue.Enqueue(n);
                        }
                    }
                }

                // 过滤噪声（最小 2x2 概率图格）
                if (maxX - minX < 2 || maxY - minY < 1)
                    continue;

                // 映射回原图 + 轻量外扩（近似 unclip）
                var scaleX = (float)image.Width / mapW;
                var scaleY = (float)image.Height / mapH;
                var padX = 3f * scaleX;
                var padY = 3f * scaleY;
                var bx = Math.Max(0, minX * scaleX - padX);
                var by = Math.Max(0, minY * scaleY - padY);
                var bw = Math.Min(image.Width - bx, (maxX + 1) * scaleX + padX - bx);
                var bh = Math.Min(image.Height - by, (maxY + 1) * scaleY + padY - by);
                if (bw >= 4 && bh >= 4)
                    boxes.Add((bx, by, bw, bh));
            }
        }

        // 阅读序排序：行分组（容差 = 半行高）→ 行内按 X
        boxes.Sort((a, b) => a.Y.CompareTo(b.Y));
        var ordered = new List<(float X, float Y, float W, float H)>();
        var row = new List<(float X, float Y, float W, float H)>();
        float rowBottom = -1;
        foreach (var box in boxes)
        {
            if (row.Count > 0 && box.Y > rowBottom)
            {
                FlushRow();
            }
            row.Add(box);
            rowBottom = Math.Max(rowBottom, box.Y + box.H / 2);
        }
        FlushRow();
        return ordered;

        void FlushRow()
        {
            row.Sort((a, b) => a.X.CompareTo(b.X));
            ordered.AddRange(row);
            row.Clear();
        }
    }

    private static readonly (int, int)[] NeighborOffsets =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
    ];

    // ===== 识别 =====
    private string RecognizeCrop(Bitmap image, (float X, float Y, float W, float H) box)
    {
        var cropX = (int)Math.Floor(box.X);
        var cropY = (int)Math.Floor(box.Y);
        var cropW = Math.Min((int)Math.Ceiling(box.W), image.Width - cropX);
        var cropH = Math.Min((int)Math.Ceiling(box.H), image.Height - cropY);
        if (cropW < 2 || cropH < 2)
            return string.Empty;

        using var crop = image.Clone(new Rectangle(cropX, cropY, cropW, cropH), PixelFormat.Format24bppRgb);
        var targetW = Math.Clamp((int)Math.Round((float)cropW * RecHeight / cropH), 16, RecMaxWidth);
        targetW = Math.Max(16, targetW / 8 * 8);
        using var resized = new Bitmap(crop, targetW, RecHeight);

        var input = BuildTensor(resized, targetW, RecHeight);
        using var results = _rec.Run([NamedOnnxValue.CreateFromTensor(_recInput, input)]);
        var output = (DenseTensor<float>)results.First().AsTensor<float>();
        // [1, T, C] 贪心 CTC（blank = 0）
        var tLen = output.Dimensions[1];
        var cLen = output.Dimensions[2];
        var span = output.Buffer.Span;

        var sb = new System.Text.StringBuilder();
        var prev = 0;
        for (var t = 0; t < tLen; t++)
        {
            var offset = t * cLen;
            var best = 0;
            var bestScore = float.NegativeInfinity;
            for (var c = 0; c < cLen; c++)
            {
                if (span[offset + c] > bestScore)
                {
                    bestScore = span[offset + c];
                    best = c;
                }
            }
            if (best != 0 && best != prev && best < _vocab.Length)
                sb.Append(_vocab[best]);
            prev = best;
        }
        return sb.ToString().Trim();
    }

    /// <summary>CHW float 归一化张量（RGB 通道）</summary>
    private static DenseTensor<float> BuildTensor(Bitmap bitmap, int width, int height)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var tensor = new DenseTensor<float>([1, 3, height, width]);
        try
        {
            var row = new byte[width * 3];
            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                var rOff = (0 * height + y) * width;
                var gOff = (1 * height + y) * width;
                var bOff = (2 * height + y) * width;
                for (var x = 0; x < width; x++)
                {
                    // 24bpp 内存布局 BGR
                    var b = row[x * 3] / 255f;
                    var g = row[x * 3 + 1] / 255f;
                    var r = row[x * 3 + 2] / 255f;
                    tensor[rOff + x] = (r - Mean[0]) / Std[0];
                    tensor[gOff + x] = (g - Mean[1]) / Std[1];
                    tensor[bOff + x] = (b - Mean[2]) / Std[2];
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return tensor;
    }

    public void Dispose()
    {
        _det.Dispose();
        _rec.Dispose();
    }
}
