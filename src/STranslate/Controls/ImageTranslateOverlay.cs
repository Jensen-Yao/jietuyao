using STranslate.Core;
using STranslate.Helpers;
using System.Windows;
using System.Windows.Media;

namespace STranslate.Controls;

/// <summary>
/// 在原图坐标系中绘制图片翻译译文的 retained-mode 矢量覆盖层。
/// </summary>
public sealed class ImageTranslateOverlay : FrameworkElement
{
    public ImageTranslateOverlayDocument? Document
    {
        get => (ImageTranslateOverlayDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(ImageTranslateOverlayDocument),
            typeof(ImageTranslateOverlay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (Document is not { IsEmpty: false } document)
            return;

        ImageTranslateRenderer.DrawTranslatedOverlay(drawingContext, document);
    }
}
