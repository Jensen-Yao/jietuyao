using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;
using STranslate.ViewModels;
using System.Windows;
using System.Windows.Media;

namespace STranslate.Tests;

public class ImageTranslateTextOverlayLayoutTests
{
    [Fact]
    public void MultilineShortTranslationUsesRegionFillFontSize()
    {
        var block = Block(
            Box(0, 0, 1000, 150),
            Box(0, 0, 900, 30),
            Box(0, 36, 900, 30),
            Box(0, 72, 900, 30));

        var plan = CreatePlan(
            block,
            new Rect(0, 0, 1000, 150),
            (fontSize, _) => new Size(
                fontSize * 4,
                fontSize * ImageTranslateTextOverlayPlan.MultilineLineHeightScale));

        Assert.True(plan.IsMultiLine);
        Assert.False(plan.ShouldTrim);
        Assert.True(plan.FontSize > 30 * 0.90);
        Assert.InRange(
            plan.FontSize,
            plan.TextRect.Height / ImageTranslateTextOverlayPlan.MultilineLineHeightScale - 0.5,
            plan.TextRect.Height / ImageTranslateTextOverlayPlan.MultilineLineHeightScale + 0.5);
    }

    [Fact]
    public void MultilineLongTranslationShrinksToFitRegion()
    {
        const double textUnits = 90;
        var block = Block(
            Box(0, 0, 1000, 150),
            Box(0, 0, 900, 30),
            Box(0, 36, 900, 30),
            Box(0, 72, 900, 30));

        var plan = CreatePlan(
            block,
            new Rect(0, 0, 1000, 150),
            (fontSize, textRect) => MeasureWrappedText(fontSize, textRect, textUnits));

        var measured = MeasureWrappedText(plan.FontSize, plan.TextRect, textUnits);

        Assert.True(plan.IsMultiLine);
        Assert.False(plan.ShouldTrim);
        Assert.True(plan.FontSize > 30 * 0.90);
        Assert.True(plan.FontSize < ImageTranslateTextOverlayPlan.MaxFontSize);
        Assert.True(measured.Height <= plan.TextRect.Height + 0.1);
    }

    [Fact]
    public void MultilineUsesParagraphAndLineBoundsUnionForRegionFill()
    {
        const double textUnits = 600;
        var paragraphRect = new Rect(0, 0, 1000, 80);
        var block = Block(
            Box(0, 0, 1000, 80),
            Box(0, 0, 900, 30),
            Box(0, 50, 900, 30),
            Box(0, 100, 900, 30),
            Box(0, 150, 900, 30),
            Box(0, 200, 900, 30));

        var plan = CreatePlan(
            block,
            paragraphRect,
            (fontSize, textRect) => MeasureWrappedText(fontSize, textRect, textUnits));
        var measured = MeasureWrappedText(plan.FontSize, plan.TextRect, textUnits);

        Assert.Equal(230, plan.BoundingRect.Height, precision: 3);
        Assert.True(plan.TextRect.Height > paragraphRect.Height * 2);
        Assert.True(plan.FontSize > 15);
        Assert.False(plan.ShouldTrim);
        Assert.True(measured.Height <= plan.TextRect.Height + 0.1);
    }

    [Fact]
    public void MultilineUsesRemainingRegionHeightForLineSpacing()
    {
        var block = Block(
            Box(0, 0, 1000, 240),
            Box(0, 0, 900, 30),
            Box(0, 50, 900, 30),
            Box(0, 100, 900, 30),
            Box(0, 150, 900, 30),
            Box(0, 200, 900, 30));

        var plan = CreatePlan(
            block,
            new Rect(0, 0, 1000, 240),
            (fontSize, textRect) =>
            {
                var lineCount = fontSize <= 22 ? 7 : 12;
                return new Size(textRect.Width, lineCount * fontSize * ImageTranslateTextOverlayPlan.MultilineLineHeightScale);
            });

        Assert.True(plan.LineHeight > plan.FontSize * ImageTranslateTextOverlayPlan.MultilineLineHeightScale);
        Assert.True(plan.LineHeight * 7 >= plan.TextRect.Height * 0.90);
        Assert.True(plan.LineHeight <= plan.FontSize * 2);
    }

    [Fact]
    public void MultilinePrioritizesLargerFontWithCompactFitLineHeight()
    {
        const double textUnits = 550;
        var block = Block(
            Box(0, 0, 1000, 240),
            Box(0, 0, 900, 30),
            Box(0, 50, 900, 30),
            Box(0, 100, 900, 30),
            Box(0, 150, 900, 30),
            Box(0, 200, 900, 30));

        var plan = CreatePlan(
            block,
            new Rect(0, 0, 1000, 240),
            (fontSize, textRect) => MeasureWrappedText(fontSize, textRect, textUnits));
        var measured = MeasureWrappedText(plan.FontSize, plan.TextRect, textUnits);

        Assert.True(plan.FontSize > ImageTranslateTextOverlayPlan.MinFontSize * 2);
        Assert.True(plan.LineHeight >= plan.FontSize * ImageTranslateTextOverlayPlan.MultilineLineHeightScale);
        Assert.True(plan.LineHeight <= plan.FontSize * 2);
        Assert.True(measured.Height <= plan.TextRect.Height + 0.1);
    }

    [Fact]
    public void SingleLineTitleFontSizeUsesExpandedRegionHeight()
    {
        var block = Block(
            Box(0, 0, 400, 80),
            Box(0, 0, 380, 50));

        var plan = CreatePlan(block, new Rect(0, 0, 400, 80), (_, _) => new Size(20, 20));

        Assert.False(plan.IsMultiLine);
        Assert.Equal(plan.TextClipRect.Height * 1.2, plan.FontSize, precision: 3);
    }

    [Fact]
    public void EraseRectsExpandLineBoxesAndUseAdaptiveOverlayBackground()
    {
        var block = Block(
            Box(0, 0, 200, 60),
            Box(10, 20, 100, 20));

        var plan = CreatePlan(block, new Rect(0, 0, 200, 60), (_, _) => new Size(10, 10));

        Assert.Equal(Color.FromArgb(235, 255, 255, 255), plan.OverlayBackgroundColor);
        Assert.Equal(Colors.Black, plan.ForegroundColor);
        Assert.Single(plan.EraseRects);
        Assert.Equal(8, plan.EraseRects[0].Left, precision: 3);
        Assert.Equal(16.4, plan.EraseRects[0].Top, precision: 3);
        Assert.Equal(104, plan.EraseRects[0].Width, precision: 3);
        Assert.Equal(27.2, plan.EraseRects[0].Height, precision: 3);
    }

    [Fact]
    public void SingleLineTextClipRectCoversExpandedEraseRect()
    {
        var block = Block(
            Box(10, 20, 100, 20),
            Box(10, 20, 100, 20));

        var plan = CreatePlan(block, new Rect(10, 20, 100, 20), (_, _) => new Size(10, 10));

        Assert.Equal(8, plan.TextClipRect.Left, precision: 3);
        Assert.Equal(16.4, plan.TextClipRect.Top, precision: 3);
        Assert.Equal(104, plan.TextClipRect.Width, precision: 3);
        Assert.Equal(27.2, plan.TextClipRect.Height, precision: 3);
        AssertCovers(plan.TextClipRect, plan.BoundingRect);
        AssertCovers(plan.TextClipRect, plan.EraseRects[0]);
        AssertCovers(plan.OverlayRect, plan.TextClipRect);
    }

    [Fact]
    public void SingleLineUsesNaturalTextHeightAndExpandedFitHeight()
    {
        var measuredRects = new List<Rect>();
        var block = Block(
            Box(10, 20, 100, 20),
            Box(10, 20, 100, 20));

        var plan = ImageTranslateTextOverlayLayout.Create(
            block,
            new Rect(10, 20, 100, 20),
            (_, textRect, _) =>
            {
                measuredRects.Add(textRect);
                return new Size(10, 10);
            });

        Assert.Equal(0, plan.LineHeight);
        Assert.Equal(1, plan.MaxLineCount);
        Assert.True(double.IsPositiveInfinity(plan.MaxTextHeight));
        Assert.All(measuredRects, rect => Assert.Equal(plan.TextClipRect.Height, rect.Height, precision: 3));
    }

    [Fact]
    public void SingleLineLongTranslationExpandsToWrappedTextBeforeTrimming()
    {
        var measuredModes = new List<bool>();
        var block = Block(
            Box(10, 100, 200, 24),
            Box(10, 100, 200, 24));

        var plan = ImageTranslateTextOverlayLayout.Create(
            block,
            new Rect(10, 100, 200, 24),
            (fontSize, textRect, isMultiLine) =>
            {
                measuredModes.Add(isMultiLine);
                return isMultiLine
                    ? MeasureWrappedText(fontSize, textRect, textUnits: 35)
                    : new Size(textRect.Width + 1, textRect.Height + 1);
            });

        Assert.Contains(false, measuredModes);
        Assert.Contains(true, measuredModes);
        Assert.True(plan.IsMultiLine);
        Assert.False(plan.ShouldTrim);
        Assert.Equal(0, plan.MaxLineCount);
        Assert.Equal(plan.TextRect.Height, plan.MaxTextHeight);
        Assert.True(plan.TextClipRect.Height > plan.BoundingRect.Height * 2);
        AssertCovers(plan.OverlayRect, plan.EraseRects[0]);
    }

    [Fact]
    public void ViewModelSingleLineMeasurementUsesNaturalWidth()
    {
        const string text = "My self media video lighting shooting tips are now publicly available";
        const double maxWidth = 260;

        var measured = ImageTranslateRenderer.MeasureFormattedText(
            text,
            fontSize: 36,
            maxWidth,
            Brushes.Black,
            pixelsPerDip: 1,
            lineHeight: 0,
            maxLineCount: 1);

        Assert.True(measured.Width > maxWidth);
    }

    [Fact]
    public void RealSingleLineLongTranslationShrinksInsteadOfTrimming()
    {
        var block = new OcrLayoutBlock
        {
            Text = "My self media video lighting shooting tips are now publicly available in less than 4 square meters of shooting space",
            BoxPoints = Box(10, 100, 960, 42),
            LineBoxPoints = [Box(10, 100, 960, 42)]
        };

        var plan = ImageTranslateTextOverlayLayout.Create(
            block,
            new Rect(10, 100, 960, 42),
            (fontSize, textRect, isMultiLine) => ImageTranslateRenderer.MeasureFormattedText(
                block.Text,
                fontSize,
                textRect.Width,
                Brushes.Black,
                pixelsPerDip: 1,
                lineHeight: isMultiLine
                    ? fontSize * ImageTranslateTextOverlayPlan.MultilineLineHeightScale
                    : 0,
                maxLineCount: isMultiLine ? 0 : 1));

        Assert.False(plan.ShouldTrim);
        Assert.True(plan.FontSize < plan.TextClipRect.Height * 1.2);
    }

    [Fact]
    public void MultilineTextClipRectCoversAllExpandedEraseRects()
    {
        var block = Block(
            Box(10, 20, 180, 46),
            Box(10, 20, 180, 20),
            Box(10, 46, 180, 20));

        var plan = CreatePlan(block, new Rect(10, 20, 180, 46), (_, _) => new Size(10, 10));

        AssertCovers(plan.TextClipRect, plan.BoundingRect);
        Assert.All(plan.EraseRects, eraseRect => AssertCovers(plan.TextClipRect, eraseRect));
        Assert.All(plan.EraseRects, eraseRect => AssertCovers(plan.OverlayRect, eraseRect));
        Assert.True(plan.TextClipRect.Top < plan.BoundingRect.Top);
        Assert.True(plan.TextClipRect.Bottom > plan.BoundingRect.Bottom);
    }

    [Fact]
    public void MultilineKeepsExplicitLineHeightAndTextHeightLimit()
    {
        var block = Block(
            Box(10, 20, 180, 46),
            Box(10, 20, 180, 20),
            Box(10, 46, 180, 20));

        var plan = CreatePlan(block, new Rect(10, 20, 180, 46), (_, _) => new Size(10, 10));

        Assert.True(plan.LineHeight > 0);
        Assert.Equal(0, plan.MaxLineCount);
        Assert.Equal(plan.TextRect.Height, plan.MaxTextHeight);
    }

    [Fact]
    public void CreatePassesLineModeToMeasureText()
    {
        var singleLineModes = new List<bool>();
        var multilineModes = new List<bool>();
        var singleLineBlock = Block(
            Box(0, 0, 100, 20),
            Box(0, 0, 100, 20));
        var multilineBlock = Block(
            Box(0, 0, 100, 46),
            Box(0, 0, 100, 20),
            Box(0, 26, 100, 20));

        ImageTranslateTextOverlayLayout.Create(
            singleLineBlock,
            new Rect(0, 0, 100, 20),
            (_, _, isMultiLine) =>
            {
                singleLineModes.Add(isMultiLine);
                return new Size(10, 10);
            });
        ImageTranslateTextOverlayLayout.Create(
            multilineBlock,
            new Rect(0, 0, 100, 46),
            (_, _, isMultiLine) =>
            {
                multilineModes.Add(isMultiLine);
                return new Size(10, 10);
            });

        Assert.All(singleLineModes, mode => Assert.False(mode));
        Assert.All(multilineModes, mode => Assert.True(mode));
    }

    [Fact]
    public void TextRectStaysInsideParagraphBoundsWithPadding()
    {
        var block = Block(
            Box(10, 20, 200, 100),
            Box(10, 20, 180, 20),
            Box(10, 46, 180, 20));

        var plan = CreatePlan(block, new Rect(10, 20, 200, 100), (_, _) => new Size(10, 10));

        Assert.Equal(11, plan.TextRect.Left);
        Assert.Equal(21, plan.TextRect.Top);
        Assert.Equal(209, plan.TextRect.Right);
        Assert.Equal(119, plan.TextRect.Bottom);
    }

    [Fact]
    public void OversizedTextAtMinimumFontSizeIsMarkedForTrimming()
    {
        var block = Block(
            Box(0, 0, 80, 20),
            Box(0, 0, 80, 20));

        var plan = CreatePlan(block, new Rect(0, 0, 80, 20), (_, textRect) => new Size(textRect.Width + 1, textRect.Height + 1));

        Assert.True(plan.ShouldTrim);
        Assert.Equal(ImageTranslateTextOverlayPlan.MinFontSize, plan.FontSize);
    }

    [Fact]
    public void MissingLineBoxesFallsBackToParagraphBoxForErase()
    {
        var block = Block(Box(5, 6, 70, 30));
        var boundingRect = new Rect(5, 6, 70, 30);

        var plan = CreatePlan(block, boundingRect, (_, _) => new Size(10, 10));

        Assert.False(plan.IsMultiLine);
        Assert.Equal(boundingRect, plan.EraseRects[0]);
        Assert.Equal(boundingRect, plan.TextClipRect);
        Assert.Equal(boundingRect, plan.OverlayRect);
        Assert.Equal(boundingRect.Height * 1.2, plan.FontSize, precision: 3);
    }

    [Fact]
    public void LightThemeUsesLightOverlayAndBlackText()
    {
        var block = Block(
            Box(0, 0, 200, 40),
            Box(0, 0, 180, 20));

        var plan = ImageTranslateTextOverlayLayout.Create(
            block,
            new Rect(0, 0, 200, 40),
            (_, _, _) => new Size(10, 10),
            ImageTranslateOverlayTheme.Light);

        Assert.Equal(Color.FromArgb(235, 255, 255, 255), plan.OverlayBackgroundColor);
        Assert.Equal(Colors.Black, plan.ForegroundColor);
    }

    [Fact]
    public void DarkThemeUsesDarkOverlayAndWhiteText()
    {
        var block = Block(
            Box(0, 0, 200, 40),
            Box(0, 0, 180, 20));

        var plan = ImageTranslateTextOverlayLayout.Create(
            block,
            new Rect(0, 0, 200, 40),
            (_, _, _) => new Size(10, 10),
            ImageTranslateOverlayTheme.Dark);

        Assert.Equal(Color.FromArgb(230, 0, 0, 0), plan.OverlayBackgroundColor);
        Assert.Equal(Colors.White, plan.ForegroundColor);
    }

    [Fact]
    public void OverlayRectCoversEraseRectsAndTextClip()
    {
        var block = Block(
            Box(10, 20, 180, 46),
            Box(10, 20, 180, 20),
            Box(10, 46, 180, 20));

        var plan = CreatePlan(block, new Rect(10, 20, 180, 46), (_, _) => new Size(10, 10));

        AssertCovers(plan.OverlayRect, plan.TextClipRect);
        Assert.All(plan.EraseRects, eraseRect => AssertCovers(plan.OverlayRect, eraseRect));
    }

    [Theory]
    [InlineData("\r\n为快速开始，请选择以下任一安装方法：\r\n", "为快速开始，请选择以下任一安装方法：")]
    [InlineData("前往PowerToys的GitHub版本页面，\n  向下滚动并选择 Assets", "前往PowerToys的GitHub版本页面，向下滚动并选择Assets")]
    [InlineData("hello\nworld", "hello world")]
    [InlineData("  \t\n  ", "")]
    public void NormalizeOverlayTextRemovesLeadingLineBreaksAndCollapsesWhitespace(string input, string expected)
    {
        var result = ImageTranslateTextOverlayLayout.NormalizeOverlayText(input);

        Assert.Equal(expected, result);
    }

    private static ImageTranslateTextOverlayPlan CreatePlan(
        OcrLayoutBlock block,
        Rect boundingRect,
        Func<double, Rect, Size> measureText) =>
        ImageTranslateTextOverlayLayout.Create(
            block,
            boundingRect,
            (fontSize, textRect, _) => measureText(fontSize, textRect),
            ImageTranslateOverlayTheme.Light);

    private static void AssertCovers(Rect outer, Rect inner)
    {
        Assert.True(outer.Left <= inner.Left);
        Assert.True(outer.Top <= inner.Top);
        Assert.True(outer.Right >= inner.Right);
        Assert.True(outer.Bottom >= inner.Bottom);
    }

    private static Size MeasureWrappedText(double fontSize, Rect textRect, double textUnits)
    {
        var lineCount = Math.Max(1, (int)Math.Ceiling(textUnits * fontSize / textRect.Width));
        return new Size(
            textRect.Width,
            lineCount * fontSize * ImageTranslateTextOverlayPlan.MultilineLineHeightScale);
    }

    private static OcrLayoutBlock Block(List<BoxPoint> boxPoints, params List<BoxPoint>[] lineBoxPoints) =>
        new()
        {
            Text = "translated text",
            BoxPoints = boxPoints,
            LineBoxPoints = [.. lineBoxPoints]
        };

    private static List<BoxPoint> Box(double left, double top, double width, double height) =>
    [
        new((float)left, (float)top),
        new((float)(left + width), (float)top),
        new((float)(left + width), (float)(top + height)),
        new((float)left, (float)(top + height))
    ];
}
