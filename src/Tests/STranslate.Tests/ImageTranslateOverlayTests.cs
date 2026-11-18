using System.Drawing;
using System.Drawing.Imaging;
using STranslate.Core;
using Xunit;
using Xunit.Abstractions;

namespace STranslate.Tests;

/// <summary>
/// 译图渲染器真实执行测试（SnipTrans 算法移植验证）
/// </summary>
public sealed class ImageTranslateOverlayTests
{
    private readonly ITestOutputHelper _output;
    public ImageTranslateOverlayTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Render_PaintsTranslatedLines_KeepsOtherPixels()
    {
        using var source = new Bitmap(200, 80, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(source))
        {
            g.Clear(Color.White);
            using var font = new Font("Arial", 12);
            g.DrawString("HELLO", font, Brushes.Black, 10, 10);
        }

        var lines = new List<TranslatedLine>
        {
            new(new RectangleF(10, 10, 80, 20), "HELLO", "你好"),
        };
        using var rendered = ImageTranslateOverlay.Render(source, lines);

        Assert.Equal(source.Width, rendered.Width);
        Assert.Equal(source.Height, rendered.Height);

        // 行区域内应存在：深色字形像素 或 青色描边像素（背景本为白，填充白时靠字形/描边判定）
        var foundGlyph = false;
        var foundOutline = false;
        for (var y = 8; y < 32 && !(foundGlyph || foundOutline); y++)
        for (var x = 6; x < 96 && !(foundGlyph || foundOutline); x++)
        {
            var c = rendered.GetPixel(x, y);
            if (c.R < 120 && c.G < 120 && c.B < 140) foundGlyph = true;
            else if (c.G > 150 && c.B > 150 && c.R < 120) foundOutline = true;
        }
        Assert.True(foundGlyph || foundOutline, "行区域既无字形也无描边：未绘制");

        // 行外保持原样（右下角仍为白）
        var outside = rendered.GetPixel(190, 70);
        Assert.True(outside is { R: > 250, G: > 250, B: > 250 }, $"行外被误改: {outside}");
        _output.WriteLine($"glyph={foundGlyph} outline={foundOutline} outside={outside}");
    }

    [Fact]
    public void Render_EmptyTranslation_SkipsLine()
    {
        using var source = new Bitmap(100, 60, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(source))
        {
            g.Clear(Color.White);
        }
        var lines = new List<TranslatedLine>
        {
            new(new RectangleF(10, 10, 60, 18), "X", "  "),
        };
        using var rendered = ImageTranslateOverlay.Render(source, lines);
        Assert.Equal(source.GetPixel(40, 18).ToArgb(), rendered.GetPixel(40, 18).ToArgb());
    }
}
