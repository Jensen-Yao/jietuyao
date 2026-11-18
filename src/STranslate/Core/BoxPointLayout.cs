using System.Windows;
using STranslate.Plugin;

namespace STranslate.Core;

/// <summary>
/// OCR 坐标点的几何布局计算工具。
/// </summary>
internal static class BoxPointLayout
{
    /// <summary>
    /// 根据坐标点集合计算外接矩形（WPF 逻辑像素坐标）。空集合返回 <see cref="Rect.Empty"/>。
    /// </summary>
    internal static Rect BoundingRect(IReadOnlyList<BoxPoint> boxPoints)
    {
        if (boxPoints.Count == 0)
            return Rect.Empty;

        var minX = boxPoints.Min(p => p.X);
        var minY = boxPoints.Min(p => p.Y);
        var maxX = boxPoints.Max(p => p.X);
        var maxY = boxPoints.Max(p => p.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
