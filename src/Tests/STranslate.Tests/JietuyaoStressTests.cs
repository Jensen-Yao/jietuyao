using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using STranslate.Core;
using STranslate.Core.ClipboardHistory;
using Xunit;
using Xunit.Abstractions;

namespace STranslate.Tests;

/// <summary>
/// 压测（真实执行 + 耗时基线）：长截图多帧拼接 / GIF 长序列导出 / 剪贴板万级写入 /
/// 4K 像素缓存。上界取宽松值防性能回归。
/// </summary>
public sealed class JietuyaoStressTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _workDir;

    public JietuyaoStressTests(ITestOutputHelper output)
    {
        _output = output;
        _workDir = Path.Combine(Path.GetTempPath(), "jietuyao_stress_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch { /* 占用 */ }
    }

    private static Bitmap FrameRows(int width, int height, int rowBase)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[width * 3];
            for (var y = 0; y < height; y++)
            {
                var abs = rowBase + y;
                row.AsSpan().Fill(0);
                for (var x = 0; x < width; x++)
                {
                    // R=低位 G=高位 → 绝对行号唯一（量化 4bit 后仍唯一到 4096 行）
                    row[x * 3 + 2] = (byte)(abs % 16 << 4);
                    row[x * 3 + 1] = (byte)((abs / 16 % 16) << 4);
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

    [Fact]
    public void Stress_Stitch_50Frames()
    {
        const int w = 320;
        const int h = 200;
        const int overlap = 40;
        var frames = new List<Bitmap>();
        for (var i = 0; i < 50; i++)
            frames.Add(FrameRows(w, h, i * (h - overlap)));

        var sw = Stopwatch.StartNew();
        var stitched = ScrollStitcher.Stitch(frames);
        sw.Stop();
        foreach (var f in frames) f.Dispose();

        var expected = h * 50 - overlap * 49; // 10040
        _output.WriteLine($"50 帧拼接 {w}x{h}: 高度={stitched.Height}（期望≈{expected}） 耗时={sw.ElapsedMilliseconds}ms");
        try
        {
            Assert.Equal(w, stitched.Width);
            Assert.InRange(stitched.Height, expected - 3, expected + 3);
            Assert.True(sw.ElapsedMilliseconds < 5000, $"拼接过慢: {sw.ElapsedMilliseconds}ms");
        }
        finally
        {
            stitched.Dispose();
        }
    }

    [Fact]
    public void Stress_GifExport_300Frames()
    {
        const int w = 480;
        const int h = 270;
        using var buffer = new GifFrameBuffer(w, h, 60);
        var seed = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(seed))
        {
            g.Clear(Color.Navy);
            g.FillEllipse(Brushes.Gold, 60, 60, 140, 140);
        }
        var swTotal = Stopwatch.StartNew();
        for (var i = 0; i < 300; i++)
            buffer.Add(seed, i * 66);
        seed.Dispose();
        var addMs = swTotal.ElapsedMilliseconds;

        var path = Path.Combine(_workDir, "stress.gif");
        var swExport = Stopwatch.StartNew();
        buffer.ExportGif(path, 6);
        swExport.Stop();
        swTotal.Stop();

        var bytes = File.ReadAllBytes(path);
        var separators = 0;
        for (var i = 0; i < bytes.Length - 10; i++)
            if (bytes[i] == 0x2C && bytes[i + 1] == 0 && bytes[i + 2] == 0)
                separators++;

        _output.WriteLine($"300 帧 GIF {w}x{h}: 入队={addMs}ms 导出={swExport.ElapsedMilliseconds}ms " +
                          $"产物={bytes.Length / 1024}KB 分隔符={separators}");
        Assert.True(separators >= 300, $"帧数不足: {separators}");
        Assert.True(bytes.Length > 200_000, "产物过小");
        Assert.True(swExport.ElapsedMilliseconds < 60_000, $"导出过慢: {swExport.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Stress_Clipboard_10k_UpsertAndQuery()
    {
        var db = Path.Combine(_workDir, "stress.db");
        var img = Path.Combine(_workDir, "img");
        using var storage = new ClipboardHistoryStorage(db, img);

        var batch = new List<ClipboardItemRecord>();
        for (var i = 0; i < 10_000; i++)
        {
            batch.Add(new ClipboardItemRecord
            {
                Type = ClipboardItemType.Text,
                Text = $"item-{i} 内容样本",
                ContentHash = ClipboardDataExchange.Hash("text\n" + i),
            });
        }
        var sw = Stopwatch.StartNew();
        storage.UpsertRange(batch, 20_000);
        sw.Stop();
        var insertMs = sw.ElapsedMilliseconds;

        // 单条实时通道对照（1k 条，逐条自动提交——采集路径的真实形态）
        sw.Restart();
        for (var i = 10_000; i < 11_000; i++)
        {
            storage.Upsert(new ClipboardItemRecord
            {
                Type = ClipboardItemType.Text,
                Text = $"live-{i}",
                ContentHash = ClipboardDataExchange.Hash("text\n" + i),
            }, 20_000);
        }
        var liveMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var (items, total) = storage.Query("item-9999");
        sw.Stop();
        _output.WriteLine($"10k 批量(单事务): {insertMs}ms；1k 单条实时通道: {liveMs}ms；搜索命中={total} 耗时={sw.ElapsedMilliseconds}ms");
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.True(insertMs < 5_000, $"批量写入过慢: {insertMs}ms");
    }

    [Fact]
    public void Stress_PixelCache_4K()
    {
        var session = JietuyaoTestSessionFactory.MakeSession(3840, 2160);
        try
        {
            var sw = Stopwatch.StartNew();
            session.BuildPixelCache();
            sw.Stop();
            _output.WriteLine($"4K 像素缓存构建: {sw.ElapsedMilliseconds}ms（一次性）");
            Assert.True(sw.ElapsedMilliseconds < 2500, $"4K 缓存构建过慢: {sw.ElapsedMilliseconds}ms");

            for (var i = 0; i < 10; i++)
            {
                var x = Random.Shared.Next(3840);
                var y = Random.Shared.Next(2160);
                var viaCache = session.GetPixelAt(new System.Windows.Point(x, y));
                var direct = session.FrozenImage.GetPixel(x, y);
                Assert.True(Math.Abs(viaCache.R - direct.R) <= 1 &&
                            Math.Abs(viaCache.G - direct.G) <= 1 &&
                            Math.Abs(viaCache.B - direct.B) <= 1,
                    $"({x},{y}) 不一致 cache={viaCache} gdi={direct}");
            }
        }
        finally
        {
            session.Dispose();
        }
    }
}

file static class JietuyaoTestSessionFactory
{
    public static ScreenshotEditorSession MakeSession(int width, int height)
    {
        var frozen = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(frozen))
        {
            for (var y = 0; y < height; y += 13)
            {
                using var pen = new Pen(Color.FromArgb(255, y % 256, (y * 3) % 256, (y * 7) % 256), 5);
                g.DrawLine(pen, 0, y, width, y);
            }
        }
        var source = ScreenshotCapturer.ToBitmapSource(frozen);
        return new ScreenshotEditorSession(
            frozen, source,
            new System.Windows.Rect(0, 0, width, height),
            [new ScreenshotMonitor { Bounds = new System.Windows.Rect(0, 0, width, height), DpiScale = 1 }],
            new ScreenshotWindowFinder(),
            new ScreenshotEditOptions());
    }
}
