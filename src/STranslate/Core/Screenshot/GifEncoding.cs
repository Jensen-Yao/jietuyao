using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace STranslate.Core;

/// <summary>鼠标特效事件（录制时间线，毫秒相对录制起点）</summary>
public enum MouseEffectKind
{
    LeftClick,
    RightClick,
    WheelUp,
    WheelDown,
}

public sealed record MouseEffectEvent(long TimeMs, int X, int Y, MouseEffectKind Kind);

/// <summary>
/// 鼠标特效渲染器（移植 v1 cursor_overlay：左键烟花涟漪 / 右键蓝色涟漪 / 滚轮箭头指示）
/// </summary>
public static class MouseEffectRenderer
{
    private const int ClickDurationMs = 550;
    private const int WheelDurationMs = 420;

    /// <summary>在帧上绘制时间点生效的所有特效（GDI+，线程安全无状态）</summary>
    public static void Draw(Graphics g, long frameTimeMs, IReadOnlyList<MouseEffectEvent> events)
    {
        foreach (var e in events)
        {
            var dt = frameTimeMs - e.TimeMs;
            switch (e.Kind)
            {
                case MouseEffectKind.LeftClick when dt is >= 0 and <= ClickDurationMs:
                    DrawRipple(g, e.X, e.Y, dt / (double)ClickDurationMs, Color.FromArgb(255, 140, 0), fireworks: true);
                    break;
                case MouseEffectKind.RightClick when dt is >= 0 and <= ClickDurationMs:
                    DrawRipple(g, e.X, e.Y, dt / (double)ClickDurationMs, Color.FromArgb(30, 144, 255), fireworks: false);
                    break;
                case MouseEffectKind.WheelUp when dt is >= 0 and <= WheelDurationMs:
                    DrawWheelArrows(g, e.X, e.Y, up: true, dt / (double)WheelDurationMs);
                    break;
                case MouseEffectKind.WheelDown when dt is >= 0 and <= WheelDurationMs:
                    DrawWheelArrows(g, e.X, e.Y, up: false, dt / (double)WheelDurationMs);
                    break;
            }
        }
    }

    /// <summary>涟漪：双环扩散 + 左键附 8 向烟花射线</summary>
    private static void DrawRipple(Graphics g, int x, int y, double p, Color color, bool fireworks)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 主环
        var alpha = (int)(210 * (1 - p));
        using (var pen = new Pen(Color.FromArgb(alpha, color), (float)(4.5 * (1 - p)) + 1f))
        {
            var r = (float)(7 + p * 36);
            g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
        }

        // 副环（延迟启动）
        var p2 = (p - 0.18) / 0.82;
        if (p2 > 0)
        {
            using var pen2 = new Pen(Color.FromArgb((int)(150 * (1 - p2)), color), (float)(2.5 * (1 - p2)) + 0.5f);
            var r2 = (float)(4 + p2 * 24);
            g.DrawEllipse(pen2, x - r2, y - r2, r2 * 2, r2 * 2);
        }

        // 中心亮点
        using (var brush = new SolidBrush(Color.FromArgb((int)(230 * (1 - p)), color)))
        {
            var rDot = (float)Math.Max(1.5, 5 * (1 - p));
            g.FillEllipse(brush, x - rDot, y - rDot, rDot * 2, rDot * 2);
        }

        // 烟花射线（仅左键，前 60% 生命周期）
        if (fireworks && p < 0.6)
        {
            var fp = p / 0.6;
            using var pen = new Pen(Color.FromArgb((int)(220 * (1 - fp)), Color.FromArgb(255, 200, 60)), (float)(2.5 * (1 - fp)) + 0.5f);
            var inner = 6 + fp * 10;
            var outer = inner + 10 + fp * 16;
            for (var i = 0; i < 8; i++)
            {
                var angle = Math.PI * 2 * i / 8 + 0.39;
                var dx = Math.Cos(angle);
                var dy = Math.Sin(angle);
                g.DrawLine(pen,
                    (float)(x + dx * inner), (float)(y + dy * inner),
                    (float)(x + dx * outer), (float)(y + dy * outer));
            }
        }
    }

    /// <summary>滚轮指示：3 个叠箭头按进度点亮（绿色）</summary>
    private static void DrawWheelArrows(Graphics g, int x, int y, bool up, double p)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const int size = 7;
        const int gap = 11;
        var dir = up ? -1 : 1;

        for (var i = 0; i < 3; i++)
        {
            var phase = p * 3 - i;
            if (phase is < 0 or > 1)
                continue;
            var alpha = (int)(235 * (1 - phase));
            using var pen = new Pen(Color.FromArgb(alpha, 30, 200, 90), 3.5f);
            var cy = y + dir * (i - 1) * gap;
            // V 形箭头
            g.DrawLine(pen, x - size, cy + dir * size / 2, x, cy - dir * size / 2);
            g.DrawLine(pen, x, cy - dir * size / 2, x + size, cy + dir * size / 2);
        }
    }
}

/// <summary>
/// GIF 录制帧缓存：采集线程压 JPEG（控内存），导出时解码进 GDI+ 多帧 GIF
/// </summary>
public sealed class GifFrameBuffer : IDisposable
{
    private readonly List<byte[]> _frames = [];
    private readonly List<long> _timestamps = [];
    private readonly object _lock = new();
    private readonly long _encoderParam;

    public int Width { get; }
    public int Height { get; }
    public int Count { get { lock (_lock) return _frames.Count; } }

    public GifFrameBuffer(int width, int height, int jpegQuality = 75)
    {
        Width = width;
        Height = height;
        _encoderParam = Math.Clamp(jpegQuality, 30, 95);
    }

    /// <summary>压缩并追加一帧</summary>
    public void Add(Bitmap frame) => Add(frame, 0);

    /// <summary>压缩并追加一帧（带录制时间戳，毫秒）</summary>
    public void Add(Bitmap frame, long timestampMs)
    {
        using var stream = new MemoryStream();
        var codec = GetJpegCodec() ?? throw new InvalidOperationException("JPEG 编码器不可用");
        using (var ep = new EncoderParameters(1))
        {
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, _encoderParam);
            frame.Save(stream, codec, ep);
        }
        lock (_lock)
        {
            _frames.Add(stream.ToArray());
            _timestamps.Add(timestampMs);
        }
    }

    /// <summary>第 index 帧的录制时间戳（毫秒，无记录时按帧序推算）</summary>
    public long FrameTimestampMs(int index, int fallbackDelayCs)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _timestamps.Count && _timestamps[index] > 0)
                return _timestamps[index];
        }
        return (long)(index * fallbackDelayCs * 10.0);
    }

    /// <summary>解码指定帧（24bpp，供预览）</summary>
    public Bitmap DecodeFrame(int index)
    {
        byte[] jpeg;
        lock (_lock)
        {
            if (index < 0 || index >= _frames.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            jpeg = _frames[index];
        }
        return Decode(jpeg);
    }

    /// <summary>导出动画 GIF（延迟单位 1/100 秒；帧区间 + 时间线叠加 + 鼠标特效）</summary>
    public void ExportGif(string path, int delayPerFrameCs = 7, Bitmap? overlay = null,
        int startFrame = 0, int endFrame = int.MaxValue,
        IReadOnlyList<MouseEffectEvent>? mouseEvents = null,
        List<(long TimeMs, Bitmap Overlay)>? timelineOverlays = null,
        Action<double>? progress = null)
    {
        List<byte[]> snapshot;
        lock (_lock)
        {
            snapshot = [.. _frames];
        }
        if (snapshot.Count == 0)
            return;

        startFrame = Math.Clamp(startFrame, 0, snapshot.Count - 1);
        endFrame = Math.Clamp(endFrame, startFrame + 1, snapshot.Count);
        var count = endFrame - startFrame;

        Bitmap RenderComposited(int index)
        {
            var frame = Decode(snapshot[index]);
            using var g = Graphics.FromImage(frame);

            // 静态全时长叠加（兼容旧路径）
            overlay?.Also(o => g.DrawImage(o, 0, 0, frame.Width, frame.Height));

            // 时间线分段标注：选取时间 ≤ 帧时间的最后一个累积变体
            if (timelineOverlays is { Count: > 0 })
            {
                var frameTime = FrameTimestampMs(index, delayPerFrameCs);
                Bitmap? chosen = null;
                foreach (var (t, o) in timelineOverlays)
                {
                    if (t <= frameTime)
                        chosen = o;
                    else
                        break;
                }
                chosen?.Also(o => g.DrawImage(o, 0, 0, frame.Width, frame.Height));
            }

            // 鼠标特效（v1 cursor_overlay 移植）
            if (mouseEvents is { Count: > 0 })
            {
                MouseEffectRenderer.Draw(g, FrameTimestampMs(index, delayPerFrameCs), mouseEvents);
            }

            return frame;
        }

        using var first = RenderComposited(startFrame);
        var firstClone = new Bitmap(first);

        // 帧延迟 + 循环次数属性
        var delayItem = (PropertyItem)Activator.CreateInstance(typeof(PropertyItem))!;
        delayItem.Id = 0x5100; // PropertyTagFrameDelay
        delayItem.Type = 4;
        var delays = new int[count];
        for (var i = 0; i < count; i++)
            delays[i] = Math.Max(1, delayPerFrameCs);
        var delayBytes = new byte[delays.Length * 4];
        Buffer.BlockCopy(delays, 0, delayBytes, 0, delayBytes.Length);
        delayItem.Value = delayBytes;
        firstClone.SetPropertyItem(delayItem);

        var loopItem = (PropertyItem)Activator.CreateInstance(typeof(PropertyItem))!;
        loopItem.Id = 0x5101; // PropertyTagLoopCount
        loopItem.Type = 4;
        loopItem.Value = [0, 0, 0, 0]; // 无限循环
        firstClone.SetPropertyItem(loopItem);

        var codec = GetGifCodec() ?? throw new InvalidOperationException("GIF 编码器不可用");

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
                firstClone.Save(stream, codec, ep);
            }

            for (var i = startFrame + 1; i < endFrame; i++)
            {
                using var frame = RenderComposited(i);
                using (var ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                    firstClone.SaveAdd(frame, ep);
                }
                progress?.Invoke((double)(i - startFrame + 1) / count);
            }

            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
                firstClone.SaveAdd(ep);
            }
        }
        firstClone.Dispose();
    }

    private static Bitmap Decode(byte[] jpeg)
    {
        using var ms = new MemoryStream(jpeg);
        var bitmap = new Bitmap(ms);
        // 统一像素格式，GDI+ SaveAdd 需要一致
        if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
            return bitmap;
        var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(converted))
        {
            g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }
        bitmap.Dispose();
        return converted;
    }

    private static ImageCodecInfo? GetJpegCodec() =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");

    private static ImageCodecInfo? GetGifCodec() =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/gif");

    public void Dispose()
    {
        lock (_lock)
        {
            _frames.Clear();
            _timestamps.Clear();
        }
    }
}

file static class BitmapExtensions
{
    public static void Also(this Bitmap bitmap, Action<Bitmap> action) => action(bitmap);
}
