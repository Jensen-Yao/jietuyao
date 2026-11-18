using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace STranslate.Core;

/// <summary>
/// GIF 录制帧缓存：采集线程压 JPEG（控内存），导出时解码进 GDI+ 多帧 GIF
/// </summary>
public sealed class GifFrameBuffer : IDisposable
{
    private readonly List<byte[]> _frames = [];
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
    public void Add(Bitmap frame)
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
        }
    }

    /// <summary>导出动画 GIF（延迟单位 1/100 秒，进度回调 0..1）</summary>
    public void ExportGif(string path, int delayPerFrameCs = 7, Action<double>? progress = null)
    {
        List<byte[]> snapshot;
        lock (_lock)
        {
            snapshot = [.. _frames];
        }
        if (snapshot.Count == 0)
            return;

        // 解码首帧
        using var first = Decode(snapshot[0]);
        var firstClone = new Bitmap(first);

        // 帧延迟 + 循环次数属性
        var delayItem = (PropertyItem)Activator.CreateInstance(typeof(PropertyItem))!;
        delayItem.Id = 0x5100; // PropertyTagFrameDelay
        delayItem.Type = 4;
        var delays = new int[snapshot.Count];
        for (var i = 0; i < snapshot.Count; i++)
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

            for (var i = 1; i < snapshot.Count; i++)
            {
                using var frame = Decode(snapshot[i]);
                using (var ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                    firstClone.SaveAdd(frame, ep);
                }
                progress?.Invoke((double)i / snapshot.Count);
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
        }
    }
}
