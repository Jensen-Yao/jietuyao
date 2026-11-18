using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace STranslate.Controls;

public partial class SnackbarContainer : UserControl, IDisposable
{
    private readonly DispatcherTimer _autoHideTimer;
    private Action? _actionCallback;
    private Storyboard? _currentShowStoryboard;
    private Storyboard? _currentHideStoryboard;
    private bool _disposed;

    public SnackbarContainer()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;

        _autoHideTimer = new DispatcherTimer();
        _autoHideTimer.Tick += AutoHideTimer_Tick;
        NoticeBarControl.CloseRequested += NoticeBarControl_CloseRequested;
        NoticeBarControl.ActionRequested += NoticeBarControl_ActionRequested;
    }

    public void Show(
        string message,
        Severity severity = Severity.Informational,
        int durationMs = 3000,
        string? actionText = null,
        Action? actionCallback = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CancelCurrentAnimations();
        StopAutoHideTimer();

        NoticeBarControl.Title = string.Empty;
        NoticeBarControl.Message = message;
        NoticeBarControl.Severity = severity;
        var hasAction = !string.IsNullOrEmpty(actionText) && actionCallback != null;
        NoticeBarControl.ActionText = hasAction ? actionText! : string.Empty;
        _actionCallback = hasAction ? actionCallback : null;

        Visibility = Visibility.Visible;
        NoticeBarHost.IsHitTestVisible = true;
        NoticeBarControl.IsOpen = true;

        _currentShowStoryboard = ((Storyboard)FindResource("ShowStoryboard")).Clone();
        _currentShowStoryboard.Completed += ShowStoryboard_Completed;
        _currentShowStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);

        if (durationMs > 0)
        {
            _autoHideTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _autoHideTimer.Start();
        }
    }

    public void Hide()
    {
        if (_disposed || Visibility != Visibility.Visible)
            return;

        CancelCurrentAnimations();
        StopAutoHideTimer();

        _currentHideStoryboard = ((Storyboard)FindResource("HideStoryboard")).Clone();
        _currentHideStoryboard.Completed += HideStoryboard_Completed;
        _currentHideStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAutoHideTimer();
        _autoHideTimer.Tick -= AutoHideTimer_Tick;
        CancelCurrentAnimations();

        NoticeBarControl.CloseRequested -= NoticeBarControl_CloseRequested;
        NoticeBarControl.ActionRequested -= NoticeBarControl_ActionRequested;
        NoticeBarControl.IsOpen = false;
        NoticeBarControl.ActionText = string.Empty;
        NoticeBarHost.IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        _actionCallback = null;
    }

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        StopAutoHideTimer();
        Hide();
    }

    private void HideStoryboard_Completed(object? sender, EventArgs e)
    {
        if (_currentHideStoryboard != null)
        {
            _currentHideStoryboard.Completed -= HideStoryboard_Completed;
            _currentHideStoryboard.Remove(this);
            _currentHideStoryboard = null;
        }

        NoticeBarHost.Opacity = 0;
        if (NoticeBarHost.RenderTransform is System.Windows.Media.TranslateTransform transform)
            transform.Y = -100;
        NoticeBarControl.IsOpen = false;
        NoticeBarControl.ActionText = string.Empty;
        NoticeBarHost.IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        _actionCallback = null;
    }

    private void ShowStoryboard_Completed(object? sender, EventArgs e)
    {
        if (_currentShowStoryboard != null)
        {
            _currentShowStoryboard.Completed -= ShowStoryboard_Completed;
            _currentShowStoryboard.Remove(this);
            _currentShowStoryboard = null;
        }

        NoticeBarHost.Opacity = 1;
        if (NoticeBarHost.RenderTransform is System.Windows.Media.TranslateTransform transform)
            transform.Y = 0;
    }

    private void StopAutoHideTimer() => _autoHideTimer.Stop();

    private void CancelCurrentAnimations()
    {
        var currentOpacity = NoticeBarHost.Opacity;
        var transform = NoticeBarHost.RenderTransform as System.Windows.Media.TranslateTransform;
        var currentOffset = transform?.Y ?? 0;

        if (_currentShowStoryboard != null)
        {
            _currentShowStoryboard.Completed -= ShowStoryboard_Completed;
            _currentShowStoryboard.Remove(this);
            _currentShowStoryboard = null;
        }

        if (_currentHideStoryboard != null)
        {
            _currentHideStoryboard.Completed -= HideStoryboard_Completed;
            _currentHideStoryboard.Remove(this);
            _currentHideStoryboard = null;
        }

        NoticeBarHost.Opacity = currentOpacity;
        if (transform != null)
            transform.Y = currentOffset;
    }

    private void NoticeBarControl_CloseRequested(object? sender, EventArgs e) => Hide();

    private void NoticeBarControl_ActionRequested(object? sender, EventArgs e)
    {
        var callback = _actionCallback;
        _actionCallback = null;
        try
        {
            callback?.Invoke();
        }
        finally
        {
            Hide();
        }
    }
}
