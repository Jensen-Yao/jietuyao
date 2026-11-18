using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace STranslate.Controls;

/// <summary>
/// 显示并管理同一类型的插件服务。
/// </summary>
public class ServicePanel : ListBox
{
    static ServicePanel()
        => DefaultStyleKeyProperty.OverrideMetadata(typeof(ServicePanel),
            new FrameworkPropertyMetadata(typeof(ServicePanel)));

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);

        if (SelectedItem is not { } selectedItem)
            return;

        // 新增服务会通过绑定更新选中项；等待布局生成其容器后才能可靠地滚动到该项。
        Dispatcher.BeginInvoke(() =>
        {
            if (ReferenceEquals(SelectedItem, selectedItem))
                ScrollIntoView(selectedItem);
        }, DispatcherPriority.Loaded);
    }

    public ICommand? ActiveReplaceCommand
    {
        get => (ICommand?)GetValue(ActiveReplaceCommandProperty);
        set => SetValue(ActiveReplaceCommandProperty, value);
    }

    public static readonly DependencyProperty ActiveReplaceCommandProperty =
        DependencyProperty.Register(
            nameof(ActiveReplaceCommand),
            typeof(ICommand),
            typeof(ServicePanel));

    public ICommand? ActiveImTranCommand
    {
        get => (ICommand?)GetValue(ActiveImTranCommandProperty);
        set => SetValue(ActiveImTranCommandProperty, value);
    }

    public static readonly DependencyProperty ActiveImTranCommandProperty =
        DependencyProperty.Register(
            nameof(ActiveImTranCommand),
            typeof(ICommand),
            typeof(ServicePanel));

    public ICommand? ActiveImTranOcrCommand
    {
        get => (ICommand?)GetValue(ActiveImTranOcrCommandProperty);
        set => SetValue(ActiveImTranOcrCommandProperty, value);
    }

    public static readonly DependencyProperty ActiveImTranOcrCommandProperty =
        DependencyProperty.Register(
            nameof(ActiveImTranOcrCommand),
            typeof(ICommand),
            typeof(ServicePanel));

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(ServicePanel));

    public ICommand? DuplicateCommand
    {
        get => (ICommand?)GetValue(DuplicateCommandProperty);
        set => SetValue(DuplicateCommandProperty, value);
    }

    public static readonly DependencyProperty DuplicateCommandProperty =
        DependencyProperty.Register(
            nameof(DuplicateCommand),
            typeof(ICommand),
            typeof(ServicePanel));

    public Service? ReplaceService
    {
        get => (Service?)GetValue(ReplaceServiceProperty);
        set => SetValue(ReplaceServiceProperty, value);
    }

    public static readonly DependencyProperty ReplaceServiceProperty =
        DependencyProperty.Register(
            nameof(ReplaceService),
            typeof(Service),
            typeof(ServicePanel));

    public Service? ImTranService
    {
        get => (Service?)GetValue(ImTranServiceProperty);
        set => SetValue(ImTranServiceProperty, value);
    }

    public static readonly DependencyProperty ImTranServiceProperty =
        DependencyProperty.Register(
            nameof(ImTranService),
            typeof(Service),
            typeof(ServicePanel));

    public Service? ImTranOcrService
    {
        get => (Service?)GetValue(ImTranOcrServiceProperty);
        set => SetValue(ImTranOcrServiceProperty, value);
    }

    public static readonly DependencyProperty ImTranOcrServiceProperty =
        DependencyProperty.Register(
            nameof(ImTranOcrService),
            typeof(Service),
            typeof(ServicePanel));
}
