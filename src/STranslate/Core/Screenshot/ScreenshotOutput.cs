using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace STranslate.Core;

/// <summary>
/// 截图输出：剪贴板（PNG + DIB V5 双格式）与文件保存（PNG/JPG/BMP）
/// </summary>
public static class ScreenshotOutput
{
    /// <summary>
    /// 复制位图到剪贴板。同时提供 PNG 流与 CF_DIB V5，
    /// 兼容 Word/Excel/Photoshop/微信等对透明度处理各异的程序。
    /// </summary>
    public static void CopyToClipboard(Bitmap bitmap)
    {
        var dataObject = new DataObject();

        // PNG 流（透明度无损）
        using (var pngStream = new MemoryStream())
        {
            bitmap.Save(pngStream, ImageFormat.Png);
            dataObject.SetData("PNG", new MemoryStream(pngStream.ToArray()), false);
        }

        // DIB V5（直连 GDI 程序）
        var dib = BuildDibV5(bitmap);
        if (dib != null)
        {
            var dibStream = new MemoryStream(dib);
            dataObject.SetData(DataFormats.Dib, dibStream, false);
        }

        // WPF 位图（兜底，部分程序只认这个）
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var source = System.Windows.Media.Imaging.BitmapFrame.Create(stream,
            System.Windows.Media.Imaging.BitmapCreateOptions.None,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        dataObject.SetImage(source);

        Clipboard.SetDataObject(dataObject, true);
    }

    /// <summary>
    /// 构建 BITMAPV5HEADER DIB（32bpp BGRA，底-上扫描，直通 alpha）
    /// </summary>
    private static byte[]? BuildDibV5(Bitmap bitmap)
    {
        try
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                const int headerSize = 124; // BITMAPV5HEADER
                var imageSize = width * height * 4;
                var dib = new byte[headerSize + imageSize];

                // BITMAPV5HEADER
                WriteInt32(dib, 0, headerSize);
                WriteInt32(dib, 4, width);
                WriteInt32(dib, 8, height); // 正值 = 底-上
                WriteInt16(dib, 12, 1);     // planes
                WriteInt16(dib, 14, 32);    // bit count
                WriteInt32(dib, 16, 0);     // BI_RGB
                WriteInt32(dib, 20, imageSize);
                WriteInt32(dib, 24, 2835);  // 72 DPI
                WriteInt32(dib, 28, 2835);
                WriteInt32(dib, 32, 0);
                WriteInt32(dib, 36, 0);
                WriteInt32(dib, 40, 0xFF0000); // red mask
                WriteInt32(dib, 44, 0x00FF00); // green mask
                WriteInt32(dib, 48, 0x0000FF); // blue mask
                WriteInt32(dib, 52, unchecked((int)0xFF000000)); // alpha mask
                WriteInt32(dib, 56, unchecked((int)0x73524742)); // 'sRGB'
                WriteInt32(dib, 108, 4);    // LCS_sRGB intent? 实际为 CSCIntent: LCS_GM_IMAGES

                // 逐行倒序 + 预乘 alpha → 直通 alpha
                var srcStride = data.Stride;
                for (var y = 0; y < height; y++)
                {
                    var srcRow = data.Scan0 + y * srcStride;
                    var dstOffset = headerSize + (height - 1 - y) * width * 4;
                    for (var x = 0; x < width; x++)
                    {
                        // Format32bppPArgb 内存布局为 BGRA 且预乘
                        var b = Marshal.ReadByte(srcRow, x * 4);
                        var g = Marshal.ReadByte(srcRow, x * 4 + 1);
                        var r = Marshal.ReadByte(srcRow, x * 4 + 2);
                        var a = Marshal.ReadByte(srcRow, x * 4 + 3);
                        if (a == 0)
                        {
                            r = g = b = 0;
                        }
                        else
                        {
                            r = (byte)Math.Min(255, r * 255 / a);
                            g = (byte)Math.Min(255, g * 255 / a);
                            b = (byte)Math.Min(255, b * 255 / a);
                        }
                        dib[dstOffset + x * 4] = b;
                        dib[dstOffset + x * 4 + 1] = g;
                        dib[dstOffset + x * 4 + 2] = r;
                        dib[dstOffset + x * 4 + 3] = a;
                    }
                }
                return dib;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        catch
        {
            return null;
        }
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    #region PDF 导出（最小 PDF 写入器）

    /// <summary>
    /// 位图导出为单页 PDF（RGB FlateDecode 嵌入，300 视觉无损）
    /// </summary>
    public static void SaveToPdf(Bitmap bitmap, string path)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        // 提取 RGB 字节
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        byte[] rgb;
        try
        {
            rgb = new byte[width * height * 3];
            var stride = Math.Abs(data.Stride);
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, rgb, y * width * 3, width * 3);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var compressed = ZlibCompress(rgb);

        using var output = new MemoryStream();
        var offsets = new long[6];

        void Write(string s)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            output.Write(bytes);
        }

        Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        offsets[1] = output.Length;
        Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = output.Length;
        Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = output.Length;
        Write($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] " +
              $"/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        offsets[4] = output.Length;
        Write($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {width} /Height {height} " +
              $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
        output.Write(compressed);
        Write("\nendstream\nendobj\n");

        var content = $"q {width} 0 0 {height} 0 0 cm /Im0 Do Q";
        offsets[5] = output.Length;
        Write($"5 0 obj\n<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n");

        var xrefPos = output.Length;
        Write($"xref\n0 6\n0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++)
            Write($"{offsets[i]:D10} 00000 n \n");
        Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");

        File.WriteAllBytes(path, output.ToArray());
    }

    /// <summary>zlib 格式压缩（PDF FlateDecode 要求 zlib 头 + Adler32）</summary>
    private static byte[] ZlibCompress(byte[] data)
    {
        using var deflate = new MemoryStream();
        using (var compressor = new System.IO.Compression.DeflateStream(deflate, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(data);
        }
        var deflated = deflate.ToArray();

        var result = new byte[deflated.Length + 6];
        result[0] = 0x78;
        result[1] = 0x9C;
        deflated.CopyTo(result, 2);
        var adler = Adler32(data);
        result[^4] = (byte)(adler >> 24);
        result[^3] = (byte)(adler >> 16);
        result[^2] = (byte)(adler >> 8);
        result[^1] = (byte)adler;
        return result;
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    #endregion

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    /// <summary>
    /// GIF 保存对话框（录制完成用），返回路径或 null
    /// </summary>
    public static string? SaveGifFile(string suggestedName, string? initialDirectory = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存 GIF",
            Filter = "GIF 动画 (*.gif)|*.gif",
            FileName = suggestedName,
            AddExtension = true,
        };
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// 弹出保存对话框并保存。返回保存路径，取消返回 null。
    /// </summary>
    public static string? SaveToFile(Bitmap bitmap, string? initialDirectory = null, string? suggestedName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存截图",
            Filter = "PNG 图像 (*.png)|*.png|JPEG 图像 (*.jpg)|*.jpg|位图 (*.bmp)|*.bmp|PDF 文档 (*.pdf)|*.pdf",
            FileName = suggestedName ?? $"jietuyao_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            AddExtension = true,
        };
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        if (dialog.ShowDialog() != true)
            return null;

        if (dialog.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            SaveToPdf(bitmap, dialog.FileName);
            return dialog.FileName;
        }

        var encoder = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => (System.Windows.Media.Imaging.BitmapEncoder)new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new System.Windows.Media.Imaging.BmpBitmapEncoder(),
            _ => new System.Windows.Media.Imaging.PngBitmapEncoder(),
        };

        using var input = new MemoryStream();
        bitmap.Save(input, ImageFormat.Png);
        input.Position = 0;
        var frame = System.Windows.Media.Imaging.BitmapFrame.Create(input);
        encoder.Frames.Add(frame);
        using var output = File.Create(dialog.FileName);
        encoder.Save(output);
        return dialog.FileName;
    }
}
