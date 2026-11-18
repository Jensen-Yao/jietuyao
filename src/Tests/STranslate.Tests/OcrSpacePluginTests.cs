using STranslate.Plugin;
using STranslate.Plugin.Ocr.OcrSpace;

namespace STranslate.Tests;

public class OcrSpacePluginTests
{
    [Theory]
    [InlineData(LangEnum.Auto, "auto")]
    [InlineData(LangEnum.ChineseSimplified, "chs")]
    [InlineData(LangEnum.ChineseTraditional, "cht")]
    [InlineData(LangEnum.Cantonese, "cht")]
    [InlineData(LangEnum.English, "eng")]
    [InlineData(LangEnum.Japanese, "jpn")]
    [InlineData(LangEnum.Korean, "kor")]
    [InlineData(LangEnum.French, "fre")]
    [InlineData(LangEnum.Spanish, "spa")]
    [InlineData(LangEnum.Russian, "rus")]
    [InlineData(LangEnum.German, "ger")]
    [InlineData(LangEnum.Italian, "ita")]
    [InlineData(LangEnum.Turkish, "tur")]
    [InlineData(LangEnum.PortuguesePortugal, "por")]
    [InlineData(LangEnum.PortugueseBrazil, "por")]
    [InlineData(LangEnum.Vietnamese, "vnm")]
    [InlineData(LangEnum.Thai, "tha")]
    [InlineData(LangEnum.Arabic, "ara")]
    [InlineData(LangEnum.Swedish, "swe")]
    [InlineData(LangEnum.Dutch, "dut")]
    [InlineData(LangEnum.Polish, "pol")]
    [InlineData(LangEnum.Ukrainian, "ukr")]
    public void LangConverter_MapsToOcrSpaceCode(LangEnum lang, string expected)
    {
        var plugin = new Main();
        var code = plugin.LangConverter(lang);
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(LangEnum.Malay)]
    [InlineData(LangEnum.Hindi)]
    [InlineData(LangEnum.Indonesian)]
    [InlineData(LangEnum.MongolianCyrillic)]
    [InlineData(LangEnum.Khmer)]
    [InlineData(LangEnum.NorwegianBokmal)]
    [InlineData(LangEnum.NorwegianNynorsk)]
    [InlineData(LangEnum.Persian)]
    [InlineData(LangEnum.Uzbek)]
    [InlineData(LangEnum.Uyghur)]
    public void LangConverter_UnsupportedFallsBackToAuto(LangEnum lang)
    {
        var plugin = new Main();
        Assert.Equal("auto", plugin.LangConverter(lang));
    }

    [Fact]
    public void GetBase64ImagePrefix_DetectsPng()
    {
        // PNG 魔数：89 50 4E 47
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2 };
        Assert.Equal("data:image/png;base64,", Main.GetBase64ImagePrefix(png));
    }

    [Fact]
    public void GetBase64ImagePrefix_DetectsJpeg()
    {
        // JPEG 魔数：FF D8 FF
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        Assert.Equal("data:image/jpeg;base64,", Main.GetBase64ImagePrefix(jpeg));
    }

    [Fact]
    public void GetBase64ImagePrefix_DefaultsToJpegForUnknown()
    {
        var unknown = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.Equal("data:image/jpeg;base64,", Main.GetBase64ImagePrefix(unknown));
    }

    [Fact]
    public void BuildContentsFromLines_BuildsLineBoundingBoxFromWords()
    {
        // 一行三词：左 (0,0)宽5高5 / 中 (10,10)宽5高5 / 右 (20,10)宽5高5
        // 外接矩形：minLeft=0, minTop=0, maxRight=25, maxBottom=15
        var lines = new List<Main.Line>
        {
            new()
            {
                LineText = "A B C",
                Words =
                [
                    new Main.Word { WordText = "A", Left = 0, Top = 0, Width = 5, Height = 5 },
                    new Main.Word { WordText = "B", Left = 10, Top = 10, Width = 5, Height = 5 },
                    new Main.Word { WordText = "C", Left = 20, Top = 10, Width = 5, Height = 5 }
                ]
            }
        };

        var contents = Main.BuildContentsFromLines(lines);

        Assert.Single(contents);
        Assert.Equal("A B C", contents[0].Text);
        Assert.Equal(4, contents[0].BoxPoints.Count);
        AssertBoxPoint(contents[0].BoxPoints[0], 0, 0);    // 左上
        AssertBoxPoint(contents[0].BoxPoints[1], 25, 0);   // 右上
        AssertBoxPoint(contents[0].BoxPoints[2], 25, 15);  // 右下
        AssertBoxPoint(contents[0].BoxPoints[3], 0, 15);   // 左下
    }

    private static void AssertBoxPoint(BoxPoint point, float x, float y)
    {
        Assert.Equal(x, point.X);
        Assert.Equal(y, point.Y);
    }

    [Fact]
    public void BuildContentsFromLines_KeepsReadingOrderAcrossLines()
    {
        var lines = new List<Main.Line>
        {
            new() { LineText = "first", Words = [new Main.Word { WordText = "first", Left = 0, Top = 0, Width = 5, Height = 5 }] },
            new() { LineText = "second", Words = [new Main.Word { WordText = "second", Left = 10, Top = 10, Width = 5, Height = 5 }] }
        };

        var contents = Main.BuildContentsFromLines(lines);

        Assert.Equal(["first", "second"], contents.Select(c => c.Text));
    }

    [Fact]
    public void BuildContentsFromLines_KeepsTextWhenWordsMissing()
    {
        // LineText 有值但 Words 缺失时仍保留行文本，不添加坐标框
        var lines = new List<Main.Line>
        {
            new() { LineText = "only text" }
        };

        var contents = Main.BuildContentsFromLines(lines);

        Assert.Single(contents);
        Assert.Equal("only text", contents[0].Text);
        Assert.Empty(contents[0].BoxPoints);
    }

    [Fact]
    public void BuildContentsFromLines_SkipsEmptyLineText()
    {
        var lines = new List<Main.Line>
        {
            new() { LineText = "   ", Words = [new Main.Word { WordText = "x", Left = 0, Top = 0, Width = 1, Height = 1 }] },
            new() { LineText = "ok", Words = [new Main.Word { WordText = "ok", Left = 0, Top = 0, Width = 1, Height = 1 }] }
        };

        var contents = Main.BuildContentsFromLines(lines);

        Assert.Single(contents);
        Assert.Equal("ok", contents[0].Text);
    }

    [Fact]
    public void BuildContentsFromLines_ReturnsEmptyForNullOrEmptyLines()
    {
        Assert.Empty(Main.BuildContentsFromLines(null));
        Assert.Empty(Main.BuildContentsFromLines(new List<Main.Line>()));
    }
}
