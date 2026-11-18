using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace STranslate.Core;

/// <summary>
/// 将 JietuyaoIcons 的 SVG 路径数据渲染为 WPF 元素（描边色可覆盖）
/// </summary>
public static class JietuyaoIconRenderer
{
    /// <summary>
    /// 渲染图标。stroke 为描边/填充色，mirror 水平翻转（如重做=镜像撤销）
    /// </summary>
    public static UIElement Create(JietuyaoIcons.IconDef def, Brush stroke, double size = 16, bool mirror = false)
    {
        var canvas = new Canvas
        {
            Width = def.Width,
            Height = def.Height,
        };
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(-def.MinX, -def.MinY));
        if (mirror)
            transform.Children.Add(new ScaleTransform(-1, 1, def.Width / 2, def.Height / 2));
        canvas.RenderTransform = transform;

        foreach (var p in def.Paths)
        {
            var path = new Path
            {
                Data = Geometry.Parse(p.Data),
                StrokeThickness = p.StrokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            if (p.IsFill)
            {
                path.Fill = stroke;
            }
            else
            {
                path.Stroke = stroke;
            }
            canvas.Children.Add(path);
        }

        return new Viewbox
        {
            Child = canvas,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }
}
