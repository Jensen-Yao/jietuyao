using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

/// <summary>
/// CardExpander 的附加属性帮助类，用于根据数据状态自动控制展开/折叠行为
/// </summary>
public static class ExpanderAttach
{
    /// <summary>
    /// 用于控制 CardExpander 是否展开的附加属性
    /// 当绑定的数据结果为 true 时，自动展开 CardExpander
    /// </summary>
    public static readonly DependencyProperty HasResultProperty =
        DependencyProperty.RegisterAttached(
            "HasResult",
            typeof(bool),
            typeof(ExpanderAttach),
            new PropertyMetadata(false, OnHasResultChanged));

    /// <summary>
    /// 标记是否为自动展开的附加属性
    /// </summary>
    public static readonly DependencyProperty IsAutoExpandedProperty =
        DependencyProperty.RegisterAttached(
            "IsAutoExpanded",
            typeof(bool),
            typeof(ExpanderAttach),
            new PropertyMetadata(false));

    /// <summary>
    /// 设置 HasResult 附加属性值
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <param name="value">是否有结果</param>
    public static void SetHasResult(UIElement element, bool value)
        => element.SetValue(HasResultProperty, value);

    /// <summary>
    /// 获取 HasResult 附加属性值
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <returns>是否有结果</returns>
    public static bool GetHasResult(UIElement element)
        => (bool)element.GetValue(HasResultProperty);

    /// <summary>
    /// 设置 IsAutoExpanded 附加属性值
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <param name="value">是否为自动展开</param>
    public static void SetIsAutoExpanded(UIElement element, bool value)
        => element.SetValue(IsAutoExpandedProperty, value);

    /// <summary>
    /// 获取 IsAutoExpanded 附加属性值
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <returns>是否为自动展开</returns>
    public static bool GetIsAutoExpanded(UIElement element)
        => (bool)element.GetValue(IsAutoExpandedProperty);

    /// <summary>
    /// HasResult 属性变更时的回调方法
    /// </summary>
    /// <param name="d">依赖对象</param>
    /// <param name="e">属性变更事件参数</param>
    private static void OnHasResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Expander expander)
            return;

        // 获取新的属性值
        var hasResult = (bool)e.NewValue;

        // 根据是否有结果来控制展开状态
        // 有结果时展开，无结果时保持折叠
        if (hasResult)
        {
            // 标记为自动展开
            SetIsAutoExpanded(expander, true);
            expander.IsExpanded = true;

            //TODO: 如果有问题考虑解除注释 延迟清除标记，让事件处理器能够检测到
            //expander.Dispatcher.BeginInvoke(() =>
            //{
            //    SetIsAutoExpanded(expander, false);
            //}, System.Windows.Threading.DispatcherPriority.Input);
        }
        else
        {
            // 折叠时清除自动展开标记
            SetIsAutoExpanded(expander, false);
            expander.IsExpanded = false;
        }
    }
}