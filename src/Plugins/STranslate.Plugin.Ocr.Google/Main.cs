using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.Google.View;
using STranslate.Plugin.Ocr.Google.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.Google;

public class Main : ObservableObject, IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages => Enum.GetValues<LangEnum>();

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() => _viewModel?.Dispose();

    public string? GetLanguage(LangEnum langEnum) => null;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        // 构造请求 URL: {Url}/v1/images:annotate?key={ApiKey}
        var uriBuilder = new UriBuilder(Settings.Url)
        {
            Path = "/v1/images:annotate",
            Query = $"key={Settings.ApiKey}"
        };

        // 构造请求体
        var base64Str = Convert.ToBase64String(request.ImageData);
        const string ocrType = "TEXT_DETECTION";
        var content = new
        {
            requests = new[]
            {
                new
                {
                    features = new[] { new { type = ocrType } },
                    image = new { content = base64Str }
                }
            }
        };

        // 发起请求
        var resp = await Context.HttpService.PostAsync(uriBuilder.Uri.ToString(), content, null, cancellationToken);
        if (string.IsNullOrEmpty(resp))
            throw new Exception("请求结果为空");

        // 解析响应
        var parsedData = JsonNode.Parse(resp) ?? throw new Exception($"反序列化失败: {resp}");

        // 判断是否出错
        if (parsedData["error"] is JsonNode error)
            return new OcrResult().Fail(error["message"]?.ToString() ?? "unknown error");

        // 提取词级结果（跳过第 0 项整图聚合文本）
        var textAnnotations = parsedData["responses"]?[0]?["textAnnotations"] as JsonArray;
        var ocrResult = new OcrResult();
        if (textAnnotations is null || textAnnotations.Count == 0)
            return ocrResult;

        for (var i = 1; i < textAnnotations.Count; i++)
        {
            var annotation = textAnnotations[i];
            if (annotation is null) continue;
            var ocrContent = new OcrContent
            {
                Text = annotation["description"]?.ToString() ?? string.Empty
            };
            foreach (var point in Converter(annotation["boundingPoly"]?["vertices"] as JsonArray))
            {
                // 仅位置不全为 0 时添加
                if (point.X != point.Y || point.X != 0)
                    ocrContent.BoxPoints.Add(new BoxPoint(point.X, point.Y));
            }
            ocrResult.OcrContents.Add(ocrContent);
        }

        return ocrResult;
    }

    /// <summary>
    ///     将 Google Vision 的 vertices 转换为 BoxPoint 列表。
    ///     vertices 顺序：[0]左上 [1]右上 [2]右下 [3]左下；为 null 时返回 4 个 (0,0)。
    /// </summary>
    private static List<BoxPoint> Converter(JsonArray? vertices)
    {
        if (vertices is null || vertices.Count == 0)
            return [new BoxPoint(0, 0), new BoxPoint(0, 0), new BoxPoint(0, 0), new BoxPoint(0, 0)];

        return
        [
            // left top
            new BoxPoint(vertices[0]?["x"]?.GetValue<float>() ?? 0, vertices[0]?["y"]?.GetValue<float>() ?? 0),
            // right top
            new BoxPoint(vertices[1]?["x"]?.GetValue<float>() ?? 0, vertices[1]?["y"]?.GetValue<float>() ?? 0),
            // right bottom
            new BoxPoint(vertices[2]?["x"]?.GetValue<float>() ?? 0, vertices[2]?["y"]?.GetValue<float>() ?? 0),
            // left bottom
            new BoxPoint(vertices[3]?["x"]?.GetValue<float>() ?? 0, vertices[3]?["y"]?.GetValue<float>() ?? 0)
        ];
    }
}
