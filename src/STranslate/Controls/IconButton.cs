using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace STranslate.Controls;

public class IconButton : Control
{
    public enum IconButtonType
    {
        /// <summary>
        /// 一次性按钮
        /// </summary>
        Once,
        /// <summary>
        /// 切换按钮
        /// </summary>
        Toggle
    }

    static IconButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(IconButton),
            new FrameworkPropertyMetadata(typeof(IconButton)));
    }

    private ToggleButton? _toggleButton;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 移除旧的事件处理器
        if (_toggleButton != null)
        {
            _toggleButton.PreviewMouseLeftButtonDown -= OnToggleButtonPreviewMouseLeftButtonDown;
        }

        // 获取模板中的 ToggleButton
        _toggleButton = GetTemplateChild("PART_ToggleButton") as ToggleButton;

        // 添加新的事件处理器
        if (_toggleButton != null)
        {
            _toggleButton.PreviewMouseLeftButtonDown += OnToggleButtonPreviewMouseLeftButtonDown;
        }
    }

    private void OnToggleButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 仅在启用 RequireCtrlToToggle 属性时才应用特殊逻辑
        if (!RequireCtrlToToggle || Type != IconButtonType.Toggle)
        {
            return;
        }

        // 检查是否按下 Ctrl 键
        bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (!isCtrlPressed)
        {
            // 普通点击：执行命令，但阻止切换状态
            e.Handled = true;

            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }
        // Ctrl + 点击：让默认行为发生（切换状态），不执行命令
    }

    public IconButtonType Type
    {
        get => (IconButtonType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register(
            nameof(Type),
            typeof(IconButtonType),
            typeof(IconButton),
            new PropertyMetadata(IconButtonType.Once));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(IconButton),
            new FrameworkPropertyMetadata(
                default, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            nameof(IconSize),
            typeof(double),
            typeof(IconButton),
            new PropertyMetadata(16.0));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(IconButton));

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public static readonly DependencyProperty IsOnProperty =
        DependencyProperty.Register(
            nameof(IsOn),
            typeof(bool),
            typeof(IconButton),
            new FrameworkPropertyMetadata(
                false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(IconButton));

    /// <summary>
    /// 获取或设置是否需要按住 Ctrl 键才能切换状态。
    /// 当为 true 时，普通点击执行 Command，Ctrl + 点击切换 IsOn 状态。
    /// 默认值为 false，保持原有的 Toggle 行为。
    /// </summary>
    public bool RequireCtrlToToggle
    {
        get => (bool)GetValue(RequireCtrlToToggleProperty);
        set => SetValue(RequireCtrlToToggleProperty, value);
    }

    public static readonly DependencyProperty RequireCtrlToToggleProperty =
        DependencyProperty.Register(
            nameof(RequireCtrlToToggle),
            typeof(bool),
            typeof(IconButton),
            new PropertyMetadata(false));
}