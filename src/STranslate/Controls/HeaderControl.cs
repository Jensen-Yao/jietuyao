using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using STranslate.ViewModels;

namespace STranslate.Controls;

public class HeaderControl : Control
{
    private Border? _dragBorder;

    static HeaderControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HeaderControl),
            new FrameworkPropertyMetadata(typeof(HeaderControl)));
    }

    public bool IsTopmost
    {
        get => (bool)GetValue(IsTopmostProperty);
        set => SetValue(IsTopmostProperty, value);
    }

    public static readonly DependencyProperty IsTopmostProperty =
        DependencyProperty.Register(
            nameof(IsTopmost),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #region ClipboardMonitor

    public bool IsClipboardMonitoring
    {
        get => (bool)GetValue(IsClipboardMonitoringProperty);
        set => SetValue(IsClipboardMonitoringProperty, value);
    }

    public static readonly DependencyProperty IsClipboardMonitoringProperty =
        DependencyProperty.Register(
            nameof(IsClipboardMonitoring),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsClipboardMonitorVisible
    {
        get => (bool)GetValue(IsClipboardMonitorVisibleProperty);
        set => SetValue(IsClipboardMonitorVisibleProperty, value);
    }

    public static readonly DependencyProperty IsClipboardMonitorVisibleProperty =
        DependencyProperty.Register(
            nameof(IsClipboardMonitorVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    #region Setting

    public bool IsSettingVisible
    {
        get => (bool)GetValue(IsSettingVisibleProperty);
        set => SetValue(IsSettingVisibleProperty, value);
    }

    public static readonly DependencyProperty IsSettingVisibleProperty =
        DependencyProperty.Register(
            nameof(IsSettingVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ICommand? SettingCommand
    {
        get => (ICommand?)GetValue(SettingCommandProperty);
        set => SetValue(SettingCommandProperty, value);
    }

    public static readonly DependencyProperty SettingCommandProperty =
        DependencyProperty.Register(
            nameof(SettingCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    #endregion

    #region Close

    /// <summary>
    /// 获取或设置固定关闭按钮触发的命令。
    /// </summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(
            nameof(CloseCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    /// <summary>
    /// 获取或设置固定关闭按钮传递给命令的参数。
    /// </summary>
    public object? CloseCommandParameter
    {
        get => GetValue(CloseCommandParameterProperty);
        set => SetValue(CloseCommandParameterProperty, value);
    }

    public static readonly DependencyProperty CloseCommandParameterProperty =
        DependencyProperty.Register(
            nameof(CloseCommandParameter),
            typeof(object),
            typeof(HeaderControl));

    /// <summary>
    /// 获取或设置固定关闭按钮是否显示。
    /// </summary>
    public bool IsCloseVisible
    {
        get => (bool)GetValue(IsCloseVisibleProperty);
        set => SetValue(IsCloseVisibleProperty, value);
    }

    public static readonly DependencyProperty IsCloseVisibleProperty =
        DependencyProperty.Register(
            nameof(IsCloseVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    #region HideInput

    public bool IsHideInput
    {
        get => (bool)GetValue(IsHideInputProperty);
        set => SetValue(IsHideInputProperty, value);
    }

    public static readonly DependencyProperty IsHideInputProperty =
        DependencyProperty.Register(
            nameof(IsHideInput),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsHideInputVisible
    {
        get => (bool)GetValue(IsHideInputVisibleProperty);
        set => SetValue(IsHideInputVisibleProperty, value);
    }

    public static readonly DependencyProperty IsHideInputVisibleProperty =
        DependencyProperty.Register(
            nameof(IsHideInputVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    #region ScreenshotTranslate

    public bool IsScreenshotTranslateVisible
    {
        get => (bool)GetValue(IsScreenshotTranslateVisibleProperty);
        set => SetValue(IsScreenshotTranslateVisibleProperty, value);
    }

    public static readonly DependencyProperty IsScreenshotTranslateVisibleProperty =
        DependencyProperty.Register(
            nameof(IsScreenshotTranslateVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ICommand? ScreenshotTranslateCommand
    {
        get => (ICommand?)GetValue(ScreenshotTranslateCommandProperty);
        set => SetValue(ScreenshotTranslateCommandProperty, value);
    }

    public static readonly DependencyProperty ScreenshotTranslateCommandProperty =
        DependencyProperty.Register(
            nameof(ScreenshotTranslateCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    #endregion

    #region ImageTranslate

    public bool IsImageTranslateVisible
    {
        get => (bool)GetValue(IsImageTranslateVisibleProperty);
        set => SetValue(IsImageTranslateVisibleProperty, value);
    }

    public static readonly DependencyProperty IsImageTranslateVisibleProperty =
        DependencyProperty.Register(
            nameof(IsImageTranslateVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ICommand? ImageTranslateCommand
    {
        get => (ICommand?)GetValue(ImageTranslateCommandProperty);
        set => SetValue(ImageTranslateCommandProperty, value);
    }

    public static readonly DependencyProperty ImageTranslateCommandProperty =
        DependencyProperty.Register(
            nameof(ImageTranslateCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    #endregion

    #region ColorScheme

    public bool IsColorSchemeVisible
    {
        get => (bool)GetValue(IsColorSchemeVisibleProperty);
        set => SetValue(IsColorSchemeVisibleProperty, value);
    }

    public static readonly DependencyProperty IsColorSchemeVisibleProperty =
        DependencyProperty.Register(
            nameof(IsColorSchemeVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ICommand? ColorSchemeCommand
    {
        get => (ICommand?)GetValue(ColorSchemeCommandProperty);
        set => SetValue(ColorSchemeCommandProperty, value);
    }

    public static readonly DependencyProperty ColorSchemeCommandProperty =
        DependencyProperty.Register(
            nameof(ColorSchemeCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    #endregion

    #region Mouse Selection Translation

    public bool IsMouseSelectionTranslationEnabled
    {
        get => (bool)GetValue(IsMouseSelectionTranslationEnabledProperty);
        set => SetValue(IsMouseSelectionTranslationEnabledProperty, value);
    }

    public static readonly DependencyProperty IsMouseSelectionTranslationEnabledProperty =
        DependencyProperty.Register(
            nameof(IsMouseSelectionTranslationEnabled),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsMouseSelectionTranslationVisible
    {
        get => (bool)GetValue(IsMouseSelectionTranslationVisibleProperty);
        set => SetValue(IsMouseSelectionTranslationVisibleProperty, value);
    }

    public static readonly DependencyProperty IsMouseSelectionTranslationVisibleProperty =
        DependencyProperty.Register(
            nameof(IsMouseSelectionTranslationVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    #region History

    public bool IsHistoryNavigationVisible
    {
        get => (bool)GetValue(IsHistoryNavigationVisibleProperty);
        set => SetValue(IsHistoryNavigationVisibleProperty, value);
    }

    public static readonly DependencyProperty IsHistoryNavigationVisibleProperty =
        DependencyProperty.Register(
            nameof(IsHistoryNavigationVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ICommand? HistoryPreviousCommand
    {
        get => (ICommand?)GetValue(HistoryPreviousCommandProperty);
        set => SetValue(HistoryPreviousCommandProperty, value);
    }

    public static readonly DependencyProperty HistoryPreviousCommandProperty =
        DependencyProperty.Register(
            nameof(HistoryPreviousCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    public ICommand? HistoryNextCommand
    {
        get => (ICommand?)GetValue(HistoryNextCommandProperty);
        set => SetValue(HistoryNextCommandProperty, value);
    }

    public static readonly DependencyProperty HistoryNextCommandProperty =
        DependencyProperty.Register(
            nameof(HistoryNextCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    #endregion

    #region OCR

    public bool IsOcrVisible
    {
        get => (bool)GetValue(IsOcrVisibleProperty);
        set => SetValue(IsOcrVisibleProperty, value);
    }

    public static readonly DependencyProperty IsOcrVisibleProperty =
        DependencyProperty.Register(
            nameof(IsOcrVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ICommand? OcrCommand
    {
        get => (ICommand?)GetValue(OcrCommandProperty);
        set => SetValue(OcrCommandProperty, value);
    }

    public static readonly DependencyProperty OcrCommandProperty =
        DependencyProperty.Register(
            nameof(OcrCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    #endregion

    #region AutoTranslate

    public bool IsAutoTranslate
    {
        get => (bool)GetValue(IsAutoTranslateProperty);
        set => SetValue(IsAutoTranslateProperty, value);
    }

    public static readonly DependencyProperty IsAutoTranslateProperty =
        DependencyProperty.Register(
            nameof(IsAutoTranslate),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsAutoTranslateVisible
    {
        get => (bool)GetValue(IsAutoTranslateVisibleProperty);
        set => SetValue(IsAutoTranslateVisibleProperty, value);
    }

    public static readonly DependencyProperty IsAutoTranslateVisibleProperty =
        DependencyProperty.Register(
            nameof(IsAutoTranslateVisible),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    #region Service Switcher

    public IEnumerable<ServiceQuickAccessItem>? QuickServiceItems
    {
        get => (IEnumerable<ServiceQuickAccessItem>?)GetValue(QuickServiceItemsProperty);
        set => SetValue(QuickServiceItemsProperty, value);
    }

    public static readonly DependencyProperty QuickServiceItemsProperty =
        DependencyProperty.Register(
            nameof(QuickServiceItems),
            typeof(IEnumerable<ServiceQuickAccessItem>),
            typeof(HeaderControl));

    public ICommand? ToggleQuickServiceCommand
    {
        get => (ICommand?)GetValue(ToggleQuickServiceCommandProperty);
        set => SetValue(ToggleQuickServiceCommandProperty, value);
    }

    public static readonly DependencyProperty ToggleQuickServiceCommandProperty =
        DependencyProperty.Register(
            nameof(ToggleQuickServiceCommand),
            typeof(ICommand),
            typeof(HeaderControl));

    public bool IsServiceSwitcherOpen
    {
        get => (bool)GetValue(IsServiceSwitcherOpenProperty);
        set => SetValue(IsServiceSwitcherOpenProperty, value);
    }

    public static readonly DependencyProperty IsServiceSwitcherOpenProperty =
        DependencyProperty.Register(
            nameof(IsServiceSwitcherOpen),
            typeof(bool),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    public IEnumerable<string>? VisibleActions
    {
        get => (IEnumerable<string>?)GetValue(VisibleActionsProperty);
        set => SetValue(VisibleActionsProperty, value);
    }

    public static readonly DependencyProperty VisibleActionsProperty =
        DependencyProperty.Register(
            nameof(VisibleActions),
            typeof(IEnumerable<string>),
            typeof(HeaderControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public override void OnApplyTemplate()
    {
        if (_dragBorder != null)
        {
            _dragBorder.MouseLeftButtonDown -= OnDragBorderMouseLeftButtonDown;
            _dragBorder = null;
        }

        base.OnApplyTemplate();

        if (GetTemplateChild("PART_Border") is Border border)
        {
            _dragBorder = border;
            _dragBorder.MouseLeftButtonDown += OnDragBorderMouseLeftButtonDown;
        }
    }

    private void OnDragBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Window.GetWindow(this)?.DragMove();
    }
}
