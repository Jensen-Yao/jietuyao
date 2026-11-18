using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using STranslate.Core;
using Xunit;
using Xunit.Abstractions;

namespace STranslate.Tests;

/// <summary>
/// 性能基准：输出实测耗时并设置宽松上界（防性能回归）。
/// 数值受机器负载影响，上界留足余量。
/// </summary>
public sealed class JietuyaoPerfTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _workDir;

    public JietuyaoPerfTests(ITestOutputHelper output)
    {
        _output = output;
        _workDir = Path.Combine(Path.GetTempPath(), "jietuyao_perf_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch { /* 占用 */ }
    }

    private static ScreenshotEditorSession MakeSession(int width, int height)
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
        var session = new ScreenshotEditorSession(
            frozen, source,
            new System.Windows.Rect(0, 0, width, height),
            [new ScreenshotMonitor { Bounds = new System.Windows.Rect(0, 0, width, height), DpiScale = 1 }],
            new ScreenshotWindowFinder(),
            new ScreenshotEditOptions());
        return session;
    }

    [Fact]
    public void GetPixelAt_WithCache_IsFastAndCorrect()
    {
        using var session = MakeSession(800, 600);
        session.BuildPixelCache();

        // 正确性：与 GDI+ GetPixel 抽样比对
        for (var i = 0; i < 20; i++)
        {
            var x = Random.Shared.Next(800);
            var y = Random.Shared.Next(600);
            var viaCache = session.GetPixelAt(new System.Windows.Point(x, y));
            var direct = session.FrozenImage.GetPixel(x, y);
            Assert.True(Math.Abs(viaCache.R - direct.R) <= 1 &&
                        Math.Abs(viaCache.G - direct.G) <= 1 &&
                        Math.Abs(viaCache.B - direct.B) <= 1,
                $"({x},{y}) cache={viaCache} gdi={direct}");
        }

        // 吞吐：10 万次读取
        var sw = Stopwatch.StartNew();
        long acc = 0;
        for (var i = 0; i < 100_000; i++)
        {
            var p = session.GetPixelAt(new System.Windows.Point(i % 800, (i * 7) % 600));
            acc += p.R;
        }
        sw.Stop();
        _output.WriteLine($"GetPixelAt x100k: {sw.ElapsedMilliseconds} ms（含缓存命中）");
        // GDI+ 直接读对照
        var sw2 = Stopwatch.StartNew();
        long acc2 = 0;
        for (var i = 0; i < 2_000; i++)
        {
            var p = session.FrozenImage.GetPixel(i % 800, (i * 7) % 600);
            acc2 += p.R;
        }
        sw2.Stop();
        _output.WriteLine($"GDI+ GetPixel x2k 对照: {sw2.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 500, $"缓存读取过慢: {sw.ElapsedMilliseconds}ms");
        Assert.NotEqual(0L, acc);
    }

    [Fact]
    public void GifFrameBuffer_AddThroughput()
    {
        const int w = 640;
        const int h = 480;
        using var buffer = new GifFrameBuffer(w, h, 75);
        using var frame = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(frame))
        {
            g.Clear(Color.Navy);
            g.FillEllipse(Brushes.Gold, 100, 100, 200, 200);
        }

        // 预热（首帧含编码器初始化）
        buffer.Add(frame, 0);

        var sw = Stopwatch.StartNew();
        const int count = 30;
        for (var i = 0; i < count; i++)
            buffer.Add(frame, i * 66);
        sw.Stop();
        var perFrame = (double)sw.ElapsedMilliseconds / count;
        _output.WriteLine($"JPEG 压缩 {w}x{h}: {perFrame:F1} ms/帧（q75）");
        // 24fps 要求 ≤41ms；宽松上界 80ms 防回归（编码线程独立，慢于上界只影响上限帧率）
        Assert.True(perFrame < 80, $"JPEG 编码过慢: {perFrame:F1}ms/帧");
        Assert.Equal(count + 1, buffer.Count);
    }

    [Fact]
    public void ScreenshotCapturer_ToBitmapSource_LargeImage()
    {
        using var bitmap = new Bitmap(1920, 1080, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(30, 90, 160));
        }
        var sw = Stopwatch.StartNew();
        var source = ScreenshotCapturer.ToBitmapSource(bitmap);
        sw.Stop();
        _output.WriteLine($"ToBitmapSource 1080p: {sw.ElapsedMilliseconds} ms");
        Assert.Equal(1920, source.PixelWidth);
        Assert.True(sw.ElapsedMilliseconds < 800, $"转换过慢: {sw.ElapsedMilliseconds}ms");
    }
}
