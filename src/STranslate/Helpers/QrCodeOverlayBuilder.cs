using STranslate.Core;
using STranslate.Plugin;
using System.Globalization;

namespace STranslate.Helpers;

internal static class QrCodeOverlayBuilder
{
    internal const int MaxDisplayTextElements = 80;
    private const double BoundsPaddingRatio = 0.25;

    internal static ImageTranslateOverlayDocument Create(
        QrCodeDecodeResult result,
        int imageWidth,
        int imageHeight,
        ImageTranslateOverlayTheme theme)
    {
        var block = CreateLayoutBlock(result, imageWidth, imageHeight);
        return block == null
            ? ImageTranslateOverlayDocument.Empty
            : ImageTranslateRenderer.CreateTranslatedOverlay([block], theme);
    }

    internal static OcrLayoutBlock? CreateLayoutBlock(
        QrCodeDecodeResult result,
        int imageWidth,
        int imageHeight)
    {
        if (!result.HasText || result.Points is not { Count: >= 3 } points ||
            imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        var minX = points.Min(point => (double)point.X);
        var maxX = points.Max(point => (double)point.X);
        var minY = points.Min(point => (double)point.Y);
        var maxY = points.Max(point => (double)point.Y);
        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 1 || height <= 1)
            return null;

        var left = Math.Clamp(minX - width * BoundsPaddingRatio, 0, imageWidth);
        var top = Math.Clamp(minY - height * BoundsPaddingRatio, 0, imageHeight);
        var right = Math.Clamp(maxX + width * BoundsPaddingRatio, 0, imageWidth);
        var bottom = Math.Clamp(maxY + height * BoundsPaddingRatio, 0, imageHeight);
        if (right - left <= 1 || bottom - top <= 1)
            return null;

        var boxPoints = CreateBoxPoints(left, top, right, bottom);
        var middleY = top + (bottom - top) / 2;
        return new OcrLayoutBlock
        {
            Text = TruncateText(result.Text!, MaxDisplayTextElements),
            BoxPoints = boxPoints,
            LineBoxPoints =
            [
                CreateBoxPoints(left, top, right, middleY),
                CreateBoxPoints(left, middleY, right, bottom)
            ]
        };
    }

    internal static string TruncateText(string text, int maxTextElements)
    {
        if (maxTextElements <= 0)
            return string.Empty;

        var normalized = ImageTranslateTextOverlayLayout.NormalizeOverlayText(text);
        var enumerator = StringInfo.GetTextElementEnumerator(normalized);
        var elements = new List<string>(Math.Min(normalized.Length, maxTextElements + 1));
        while (enumerator.MoveNext() && elements.Count <= maxTextElements)
            elements.Add(enumerator.GetTextElement());

        if (elements.Count <= maxTextElements)
            return string.Concat(elements);

        return string.Concat(elements.Take(maxTextElements)) + "…";
    }

    private static List<BoxPoint> CreateBoxPoints(double left, double top, double right, double bottom) =>
    [
        new((float)left, (float)top),
        new((float)right, (float)top),
        new((float)right, (float)bottom),
        new((float)left, (float)bottom)
    ];
}
