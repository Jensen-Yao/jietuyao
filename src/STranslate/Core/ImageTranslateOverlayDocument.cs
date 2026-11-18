using System.Windows;
using System.Windows.Media;

namespace STranslate.Core;

/// <summary>
/// 图片翻译结果的矢量覆盖文档。文档创建后不再修改，由 <see cref="Controls.ImageTranslateOverlay"/> 绘制。
/// </summary>
public sealed class ImageTranslateOverlayDocument
{
    internal static ImageTranslateOverlayDocument Empty { get; } = new([], []);

    internal ImageTranslateOverlayDocument(
        IReadOnlyList<ImageTranslateOverlayItem> items,
        IReadOnlyList<OcrWord> selectableWords)
    {
        Items = items;
        SelectableWords = selectableWords;
    }

    internal IReadOnlyList<ImageTranslateOverlayItem> Items { get; }

    internal IReadOnlyList<OcrWord> SelectableWords { get; }

    public bool IsEmpty => Items.Count == 0;
}

internal sealed record ImageTranslateOverlayItem(
    string Text,
    ImageTranslateTextOverlayPlan Plan,
    Brush BackgroundBrush,
    FormattedText ShadowText,
    FormattedText FormattedText,
    Point TextPosition);
