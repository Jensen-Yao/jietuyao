using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using STranslate.Core;
using STranslate.Core.ClipboardHistory;
using Xunit;
using Xunit.Abstractions;

namespace STranslate.Tests;

/// <summary>
/// jietuyao v2 引擎真实执行测试：拼接/GIF/PDF/DIB/WEBP/CSV/变换/鼠标特效
/// 全部跑真实数据管线（非 mock），用于验证"更好"的实际证据。
/// </summary>
public sealed class JietuyaoEngineTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _workDir;

    public JietuyaoEngineTests(ITestOutputHelper output)
    {
        _output = output;
        _workDir = Path.Combine(Path.GetTempPath(), "jietuyao_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch { /* 共享占用 */ }
    }

    // ===== 长截图拼接 =====

    [Fact]
    public void ScrollStitcher_FindBestOverlap_PerfectMatch()
    {
        // 上一帧行哈希 [A B C D]，本帧 [C D E F] → 重叠 = 2
        var prev = new ulong[] { 1, 2, 3, 4 };
        var cur = new ulong[] { 3, 4, 5, 6 };
        var overlap = ScrollStitcher.FindBestOverlap(prev, cur, currentHeight: 4, minMatch: 1);
        Assert.Equal(2, overlap);
    }

    [Fact]
    public void ScrollStitcher_FindBestOverlap_NoMatch_FallsBack()
    {
        // 100 行互不匹配 → 35% 盲拼兜底
        var prev = Enumerable.Range(1, 100).Select(i => (ulong)i).ToArray();
        var cur = Enumerable.Range(1000, 100).Select(i => (ulong)i).ToArray();
        var overlap = ScrollStitcher.FindBestOverlap(prev, cur, currentHeight: 100, minMatch: 10);
        Assert.Equal(35, overlap);
    }

    [Fact]
    public void ScrollStitcher_Stitch_SyntheticScroll()
    {
        const int width = 300;
        const int height = 200;
        const int overlapRows = 80;

        // 每行不同灰度 → 行哈希唯一；两帧共享 80 行（帧1 末尾 == 帧2 开头）
        var frame1 = MakeGradientFrame(width, height, rowBase: 0);
        var frame2 = MakeGradientFrame(width, height, rowBase: height - overlapRows);

        var stitched = ScrollStitcher.Stitch([frame1, frame2]);
        try
        {
            _output.WriteLine($"stitched: {stitched.Width}x{stitched.Height}");
            Assert.Equal(width, stitched.Width);
            // 预期高度 = 200 + 200 - 80 = 320（±2 容差）
            Assert.InRange(stitched.Height, 318, 322);
            // 内容连续性：帧1 首行灰度应保留在结果首行
            var firstPixel = stitched.GetPixel(5, 5);
            var expected = Color.FromArgb(255, 5 * 3, 5 * 3, 5 * 3);
            Assert.True(Math.Abs(firstPixel.R - expected.R) <= 1, $"first row color {firstPixel.R} vs {expected.R}");
        }
        finally
        {
            frame1.Dispose();
            frame2.Dispose();
            stitched.Dispose();
        }
    }

    private static Bitmap MakeGradientFrame(int width, int height, int rowBase)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[width * 3];
            for (var y = 0; y < height; y++)
            {
                var gray = (byte)Math.Clamp((rowBase + y) * 3 % 256, 0, 255);
                for (var x = 0; x < width; x++)
                {
                    row[x * 3] = gray;
                    row[x * 3 + 1] = gray;
                    row[x * 3 + 2] = gray;
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    // ===== GIF 导出 =====

    [Fact]
    public void GifFrameBuffer_Export_CreatesAnimatedGif()
    {
        const int w = 160;
        const int h = 120;
        using var buffer = new GifFrameBuffer(w, h, 60);
        for (var i = 0; i < 8; i++)
        {
            using var frame = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(frame))
            {
                g.Clear(Color.FromArgb(i * 28, 20, 60));
                g.FillEllipse(Brushes.Yellow, 10 + i * 12, 40, 40, 40);
            }
            buffer.Add(frame, i * 66);
        }
        Assert.Equal(8, buffer.Count);

        var path = Path.Combine(_workDir, "out.gif");
        var mouseEvents = new List<MouseEffectEvent>
        {
            new(100, 80, 60, MouseEffectKind.LeftClick),
            new(200, 40, 30, MouseEffectKind.WheelDown),
        };
        buffer.ExportGif(path, 6, null, 0, 8, mouseEvents, null);
        var bytes = File.ReadAllBytes(path);
        _output.WriteLine($"gif size: {bytes.Length} bytes");
        Assert.True(bytes.Length > 500, "GIF 过小");
        Assert.Equal("GIF8", Encoding.ASCII.GetString(bytes, 0, 4));
        // 多帧：存在多个图像分隔符 0x2C
        var separators = 0;
        for (var i = 0; i < bytes.Length - 10; i++)
        {
            if (bytes[i] == 0x2C && bytes[i + 1] == 0x00 && bytes[i + 2] == 0x00)
                separators++;
        }
        Assert.True(separators >= 8, $"帧数不足: {separators}");
    }

    // ===== 鼠标特效 =====

    [Fact]
    public void MouseEffectRenderer_NoOpOutsideWindow()
    {
        using var bitmap = new Bitmap(100, 100, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Black);
            MouseEffectRenderer.Draw(g, 60_000,
            [
                new MouseEffectEvent(10_000, 50, 50, MouseEffectKind.LeftClick),
            ]);
        }
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(50, 50).ToArgb());
    }

    [Fact]
    public void MouseEffectRenderer_DrawsInsideWindow()
    {
        using var bitmap = new Bitmap(120, 120, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Black);
            MouseEffectRenderer.Draw(g, 150,
            [
                new MouseEffectEvent(100, 60, 60, MouseEffectKind.LeftClick),
                new MouseEffectEvent(100, 30, 30, MouseEffectKind.WheelUp),
            ]);
        }
        // 左键烟花应有非黑像素出现在点击点附近
        var colored = 0;
        for (var y = 40; y < 82; y++)
            for (var x = 40; x < 82; x++)
                if (bitmap.GetPixel(x, y).GetBrightness() > 0.05f)
                    colored++;
        _output.WriteLine($"colored pixels near click: {colored}");
        Assert.True(colored > 20, "特效未绘制");
    }

    // ===== PDF / DIB / WEBP 输出 =====

    [Fact]
    public void ScreenshotOutput_SaveToPdf_ProducesValidPdf()
    {
        using var bitmap = new Bitmap(64, 48, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.SteelBlue, 8, 8, 48, 32);
        }
        var path = Path.Combine(_workDir, "out.pdf");
        ScreenshotOutput.SaveToPdf(bitmap, path);
        var text = File.ReadAllText(path, Encoding.Latin1);
        _output.WriteLine($"pdf bytes: {new FileInfo(path).Length}");
        Assert.StartsWith("%PDF", text);
        Assert.Contains("%%EOF", text);
        Assert.Contains("/Subtype /Image", text);
        Assert.Contains("/Filter /FlateDecode", text);
        var xref = text.LastIndexOf("startxref", StringComparison.Ordinal);
        Assert.True(xref > 0, "缺 startxref");
    }

    [Fact]
    public void ScreenshotOutput_BuildDibV5_HeaderAndRowOrder()
    {
        // 2x1：顶部红、底部蓝 → DIB 底-上存储首行应为蓝
        using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppPArgb);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.SetPixel(1, 0, Color.Red);
        bitmap.SetPixel(0, 1, Color.Blue);
        bitmap.SetPixel(1, 1, Color.Blue);
        var dib = ScreenshotOutput.BuildDibV5(bitmap);
        Assert.NotNull(dib);
        Assert.Equal(124, BitConverter.ToInt32(dib, 0)); // bV5Size
        Assert.Equal(2, BitConverter.ToInt32(dib, 4));   // width
        Assert.Equal(2, BitConverter.ToInt32(dib, 8));   // height 正值=底-上
        // 首行像素（124 偏移）：B=255,G=0,R=0 → 蓝
        Assert.Equal(255, dib[124]);
        Assert.Equal(0, dib[126]);
    }

    [Fact]
    public void ScreenshotOutput_SaveAsWebp_RoundTrip()
    {
        using var bitmap = new Bitmap(80, 60, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(10, 200, 100));
            g.FillEllipse(Brushes.White, 20, 15, 30, 30);
        }
        var path = Path.Combine(_workDir, "out.webp");
        ScreenshotOutput.SaveAsWebp(bitmap, path);
        _output.WriteLine($"webp bytes: {new FileInfo(path).Length}");
        using var image = SixLabors.ImageSharp.Image.Load(path);
        Assert.Equal(80, image.Width);
        Assert.Equal(60, image.Height);
        // 文件头 RIFF....WEBP
        var header = new byte[12];
        using (var fs = File.OpenRead(path))
            fs.Read(header, 0, 12);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(header, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(header, 8, 4));
    }

    // ===== 文本变换 =====

    [Theory]
    [InlineData("upper", "abc", "ABC")]
    [InlineData("lower", "ABC", "abc")]
    [InlineData("single_line", "a\r\nb", "a b")]
    [InlineData("no_space", "a b　c", "abc")]
    [InlineData("reverse", "abc", "cba")]
    public void TextTransforms_ApplyById(string id, string input, string expected)
    {
        Assert.Equal(expected, TextTransforms.ApplyById(id, input));
    }

    [Fact]
    public void TextTransforms_SqlIn_And_Lines()
    {
        Assert.Equal("('a', 'b', 3)", TextTransforms.ApplyById("sql_in", "a\nb\n3"));
        var numbered = TextTransforms.ApplyById("add_line_numbers", "x\ny");
        Assert.Contains("1. x", numbered);
        Assert.Contains("2. y", numbered);
    }

    [Fact]
    public void TextTransforms_Unknown_ReturnsNull()
    {
        Assert.Null(TextTransforms.ApplyById("nope", "x"));
    }

    // ===== 剪贴板存储 CSV 往返 =====

    [Fact]
    public void ClipboardStorage_CsvRoundTrip()
    {
        var db1 = Path.Combine(_workDir, "a.db");
        var img1 = Path.Combine(_workDir, "img1");
        using (var storage = new ClipboardHistoryStorage(db1, img1))
        {
            storage.Upsert(new ClipboardItemRecord
            {
                Type = ClipboardItemType.Text,
                Text = "hello, \"world\"\nline2",
                ContentHash = ClipboardDataExchange.Hash("text\nt1"),
            }, 100);
            storage.Upsert(new ClipboardItemRecord
            {
                Type = ClipboardItemType.Text,
                Text = "第二条,with comma",
                ContentHash = ClipboardDataExchange.Hash("text\nt2"),
            }, 100);
            storage.CreateGroup("工作", "💼");
        }

        var csv = Path.Combine(_workDir, "hist.csv");
        using (var storage = new ClipboardHistoryStorage(db1, img1))
        {
            var count = storage.ExportCsv(csv);
            Assert.Equal(2, count);
        }

        var db2 = Path.Combine(_workDir, "b.db");
        using (var storage2 = new ClipboardHistoryStorage(db2, Path.Combine(_workDir, "img2")))
        {
            var imported = storage2.ImportCsv(csv);
            Assert.Equal(2, imported);
            var (items, total) = storage2.Query("world");
            Assert.Equal(1, total);
            Assert.Contains("line2", items[0].Text);
        }
    }
}
