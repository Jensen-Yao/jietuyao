using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace STranslate.Core;

/// <summary>译图行（原图物理坐标 + 译文）</summary>
public sealed record TranslatedLine(RectangleF Bounds, string Original, string Translation);

/// <summary>
/// 译图渲染（移植自 SnipTrans ImageService.RenderTranslatedImage）：
/// 行矩形外扩 → 四角采样背景色 → 亮度自适应前景 → 半透明底 + 描边 → 行高比例字号居中绘制。
/// </summary>
public static class ImageTranslateOverlay
{
    public static Bitmap Render(Bitmap source, IReadOnlyList<TranslatedLine> lines)
    {
        var canvas = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(canvas))
        {
            graphics.DrawImageUnscaled(source, 0, 0);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            foreach (var line in lines)
            {
                var text = line.Translation?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var rect = RectangleF.FromLTRB(
                    Math.Max(0, line.Bounds.Left - 3),
                    Math.Max(0, line.Bounds.Top - 2),
                    Math.Min(canvas.Width, line.Bounds.Right + 3),
                    Math.Min(canvas.Height, line.Bounds.Bottom + 2));
                if (rect.Width < 4 || rect.Height < 4)
                    continue;

                var background = SampleBackground(source, rect);
                var foreground = background.GetBrightness() > 0.52f
                    ? Color.FromArgb(245, 20, 28, 34)
                    : Color.White;
                using var backgroundBrush = new SolidBrush(Color.FromArgb(238, background));
                using var foregroundBrush = new SolidBrush(foreground);
                using var outlinePen = new Pen(Color.FromArgb(145, 52, 209, 191), Math.Max(1f, rect.Height / 36f));

                graphics.FillRectangle(backgroundBrush, rect);
                graphics.DrawRectangle(outlinePen, rect.X, rect.Y, rect.Width, rect.Height);

                var fontSize = Math.Clamp(rect.Height * 0.58f, 8f, 34f);
                using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.LineLimit,
                };
                graphics.DrawString(text, font, foregroundBrush, rect, format);
            }
        }
        return canvas;
    }

    private static Color SampleBackground(Bitmap bitmap, RectangleF rect)
    {
        var points = new[]
        {
            new Point((int)rect.Left, (int)rect.Top),
            new Point((int)Math.Max(rect.Left, rect.Right - 1), (int)rect.Top),
            new Point((int)rect.Left, (int)Math.Max(rect.Top, rect.Bottom - 1)),
            new Point((int)Math.Max(rect.Left, rect.Right - 1), (int)Math.Max(rect.Top, rect.Bottom - 1)),
        };
        var colors = points.Select(p => bitmap.GetPixel(
            Math.Clamp(p.X, 0, bitmap.Width - 1),
            Math.Clamp(p.Y, 0, bitmap.Height - 1))).ToArray();
        return Color.FromArgb(
            colors.Sum(c => c.R) / colors.Length,
            colors.Sum(c => c.G) / colors.Length,
            colors.Sum(c => c.B) / colors.Length);
    }
}
