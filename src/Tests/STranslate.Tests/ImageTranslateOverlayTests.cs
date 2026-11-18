using STranslate.Controls;
using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace STranslate.Tests;

public class ImageTranslateOverlayTests
{
    [Fact]
    public void CreateTranslatedOverlayWithoutLocationsReturnsEmptyDocument()
    {
        var block = new OcrLayoutBlock { Text = "translated text" };

        var document = ImageTranslateRenderer.CreateTranslatedOverlay(
            [block],
            ImageTranslateOverlayTheme.Light);

        Assert.True(document.IsEmpty);
        Assert.Empty(document.Items);
        Assert.Empty(document.SelectableWords);
    }

    [Fact]
    public void CreateTranslatedOverlayKeepsSelectableWordsInSourceCoordinates()
    {
        var block = CreateBlock("ABC", left: 100, top: 50, width: 180, height: 40);

        var document = ImageTranslateRenderer.CreateTranslatedOverlay(
            [block],
            ImageTranslateOverlayTheme.Light);

        var item = Assert.Single(document.Items);
        Assert.False(document.IsEmpty);
        Assert.Equal("ABC", string.Concat(document.SelectableWords.Select(word => word.Text)));
        Assert.All(document.SelectableWords.Where(word => !word.BoundingBox.IsEmpty), word =>
        {
            Assert.True(word.BoundingBox.Left >= item.Plan.TextClipRect.Left);
            Assert.True(word.BoundingBox.Top >= item.Plan.TextClipRect.Top);
            Assert.True(word.BoundingBox.Right <= item.Plan.TextClipRect.Right);
            Assert.True(word.BoundingBox.Bottom <= item.Plan.TextClipRect.Bottom);
        });
    }

    [Fact]
    public void CreateTranslatedOverlayPreservesThemeColors()
    {
        var block = CreateBlock("译文", left: 10, top: 20, width: 160, height: 40);

        var light = ImageTranslateRenderer.CreateTranslatedOverlay(
            [block],
            ImageTranslateOverlayTheme.Light);
        var dark = ImageTranslateRenderer.CreateTranslatedOverlay(
            [block],
            ImageTranslateOverlayTheme.Dark);

        var lightItem = Assert.Single(light.Items);
        var darkItem = Assert.Single(dark.Items);
        Assert.Equal(Colors.Black, lightItem.Plan.ForegroundColor);
        Assert.Equal(Colors.White, darkItem.Plan.ForegroundColor);
        Assert.NotEqual(lightItem.Plan.OverlayBackgroundColor, darkItem.Plan.OverlayBackgroundColor);
        Assert.True(lightItem.BackgroundBrush.IsFrozen);
        Assert.True(darkItem.BackgroundBrush.IsFrozen);
    }

    [Fact]
    public void OverlayRendersDocumentAndBecomesTransparentWhenCleared()
    {
        RunOnStaThread(() =>
        {
            var document = ImageTranslateRenderer.CreateTranslatedOverlay(
                [CreateBlock("ABC", left: 20, top: 20, width: 160, height: 40)],
                ImageTranslateOverlayTheme.Light);
            var overlay = new ImageTranslateOverlay
            {
                Width = 220,
                Height = 100,
                Document = document
            };

            Assert.True(RenderHasVisiblePixels(overlay, 220, 100));

            overlay.Document = null;
            Assert.False(RenderHasVisiblePixels(overlay, 220, 100));
        });
    }

    [Fact]
    public void OverlayRendersLargeChineseTitleText()
    {
        RunOnStaThread(() =>
        {
            var document = ImageTranslateRenderer.CreateTranslatedOverlay(
                [
                    CreateBlock(
                        "我们穿越荆棘和玫瑰的旅程",
                        left: 149.648468,
                        top: 85.732590,
                        width: 1568.743286,
                        height: 78.889755)
                ],
                ImageTranslateOverlayTheme.Light);
            var overlay = new ImageTranslateOverlay
            {
                Width = 1920,
                Height = 240,
                Document = document
            };

            Assert.True(RenderHasDarkPixels(overlay, 1920, 240));
        });
    }

    [Fact]
    public void RenderDisplayImageWithoutOverlayReturnsCurrentDisplayImage()
    {
        RunOnStaThread(() =>
        {
            var displayImage = CreateSolidBitmap(240, 120, Colors.CornflowerBlue);

            var withoutDocument = ImageTranslateRenderer.RenderDisplayImage(displayImage, null);
            var withEmptyDocument = ImageTranslateRenderer.RenderDisplayImage(
                displayImage,
                ImageTranslateOverlayDocument.Empty);

            Assert.Same(displayImage, withoutDocument);
            Assert.Same(displayImage, withEmptyDocument);
        });
    }

    [Fact]
    public void RenderDisplayImageComposesOverlayAtOriginalResolution()
    {
        RunOnStaThread(() =>
        {
            var source = CreateSolidBitmap(240, 120, Colors.CornflowerBlue);
            var document = ImageTranslateRenderer.CreateTranslatedOverlay(
                [CreateBlock("Translated", left: 30, top: 25, width: 160, height: 50)],
                ImageTranslateOverlayTheme.Light);

            var result = ImageTranslateRenderer.RenderDisplayImage(source, document);

            Assert.Equal(source.PixelWidth, result.PixelWidth);
            Assert.Equal(source.PixelHeight, result.PixelHeight);
            Assert.Equal(ReadPixel(source, 5, 5), ReadPixel(result, 5, 5));
            Assert.False(ReadPixels(source).SequenceEqual(ReadPixels(result)));
            Assert.True(result.IsFrozen);
        });
    }

    [Fact]
    public void RenderDisplayImageSupportsLargeDarkChineseOverlay()
    {
        RunOnStaThread(() =>
        {
            var source = CreateSolidBitmap(1920, 240, Colors.White);
            var document = ImageTranslateRenderer.CreateTranslatedOverlay(
                [
                    CreateBlock(
                        "我们穿越荆棘和玫瑰的旅程",
                        left: 149.648468,
                        top: 85.732590,
                        width: 1568.743286,
                        height: 78.889755)
                ],
                ImageTranslateOverlayTheme.Dark);

            var result = ImageTranslateRenderer.RenderDisplayImage(source, document);

            Assert.Equal(1920, result.PixelWidth);
            Assert.Equal(240, result.PixelHeight);
            Assert.True(RenderHasDarkPixels(result));
        });
    }

    private static bool RenderHasVisiblePixels(FrameworkElement element, int width, int height)
    {
        var pixels = RenderPixels(element, width, height);
        return pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha != 0);
    }

    private static bool RenderHasDarkPixels(FrameworkElement element, int width, int height)
    {
        return HasDarkPixels(RenderPixels(element, width, height));
    }

    private static bool RenderHasDarkPixels(BitmapSource bitmap) =>
        HasDarkPixels(ReadPixels(bitmap));

    private static bool HasDarkPixels(byte[] pixels)
    {
        for (var index = 0; index < pixels.Length; index += 4)
        {
            var blue = pixels[index];
            var green = pixels[index + 1];
            var red = pixels[index + 2];
            var alpha = pixels[index + 3];

            if (alpha > 120 && red < 80 && green < 80 && blue < 80)
                return true;
        }

        return false;
    }

    private static byte[] RenderPixels(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static BitmapSource CreateSolidBitmap(int width, int height, Color color)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = color.B;
            pixels[index + 1] = color.G;
            pixels[index + 2] = color.R;
            pixels[index + 3] = color.A;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] ReadPixels(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static Color ReadPixel(BitmapSource bitmap, int x, int y)
    {
        var pixels = ReadPixels(bitmap);
        var index = (y * bitmap.PixelWidth + x) * 4;
        return Color.FromArgb(
            pixels[index + 3],
            pixels[index + 2],
            pixels[index + 1],
            pixels[index]);
    }

    private static OcrLayoutBlock CreateBlock(
        string text,
        double left,
        double top,
        double width,
        double height)
    {
        var box = Box(left, top, width, height);
        return new OcrLayoutBlock
        {
            Text = text,
            BoxPoints = box,
            LineBoxPoints = [box]
        };
    }

    private static List<BoxPoint> Box(double left, double top, double width, double height) =>
    [
        new((float)left, (float)top),
        new((float)(left + width), (float)top),
        new((float)(left + width), (float)(top + height)),
        new((float)left, (float)(top + height))
    ];

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
