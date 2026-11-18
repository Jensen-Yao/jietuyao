using STranslate.Core;
using STranslate.Helpers;
using System.DrawingCore.Imaging;
using System.Globalization;
using ZXing;
using ZXing.QrCode;
using ZXing.ZKWeb;

namespace STranslate.Tests;

public class QrCodeDecoderTests
{
    [Fact]
    public void Decode_ReturnsUtf8Content_ForQrCodeImage()
    {
        const string content = "https://stranslate.zggsong.com/测试";
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = 320,
                Height = 320,
                Margin = 2,
                CharacterSet = "UTF-8"
            }
        };

        using var bitmap = writer.Write(content);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        var result = QrCodeDecoder.Decode(stream.ToArray());

        Assert.Null(result.Error);
        Assert.True(result.HasText);
        Assert.Equal(content, result.Text);
        Assert.NotNull(result.Points);
        Assert.True(result.Points.Count >= 3);
    }

    [Fact]
    public void Decode_ReturnsError_ForInvalidImageData()
    {
        var result = QrCodeDecoder.Decode([0x01, 0x02, 0x03]);

        Assert.False(result.HasText);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("https://example.com/path", true)]
    [InlineData(" HTTP://example.com/path ", true)]
    [InlineData("file:///C:/Windows/System32", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("http:relative-path", false)]
    [InlineData("plain QR code content", false)]
    [InlineData("", false)]
    public void TryGetWebUri_AllowsOnlyHttpAndHttps(string value, bool expected)
    {
        var result = QrCodeDecoder.TryGetWebUri(value, out var uri);

        Assert.Equal(expected, result);
        Assert.Equal(expected, uri is not null);
    }

    [Fact]
    public void CreateLayoutBlock_ExpandsAndClampsDetectedBounds()
    {
        var result = new QrCodeDecodeResult(
            new string('a', QrCodeOverlayBuilder.MaxDisplayTextElements + 1),
            [new(20, 20), new(80, 20), new(20, 80)],
            null);

        var block = QrCodeOverlayBuilder.CreateLayoutBlock(result, 100, 100);

        Assert.NotNull(block);
        Assert.Equal(5, block.BoxPoints.Min(point => point.X));
        Assert.Equal(95, block.BoxPoints.Max(point => point.X));
        Assert.Equal(5, block.BoxPoints.Min(point => point.Y));
        Assert.Equal(95, block.BoxPoints.Max(point => point.Y));
        Assert.EndsWith("…", block.Text);
    }

    [Fact]
    public void TruncateText_DoesNotSplitUnicodeTextElements()
    {
        var content = string.Concat(Enumerable.Repeat("👨‍👩‍👧‍👦", 81));

        var result = QrCodeOverlayBuilder.TruncateText(content, 80);

        Assert.EndsWith("…", result);
        Assert.Equal(80, StringInfo.ParseCombiningCharacters(result[..^1]).Length);
    }

    [Fact]
    public void CreateOverlay_ExposesDisplayedTextForImageSelection()
    {
        var result = new QrCodeDecodeResult(
            "selectable QR content",
            [new(20, 20), new(80, 20), new(20, 80)],
            null);

        var document = QrCodeOverlayBuilder.Create(
            result,
            100,
            100,
            ImageTranslateOverlayTheme.Light);

        Assert.False(document.IsEmpty);
        Assert.NotEmpty(document.SelectableWords);
        Assert.Equal(
            "selectable QR content",
            string.Concat(document.SelectableWords.Select(word => word.Text)));
    }
}
