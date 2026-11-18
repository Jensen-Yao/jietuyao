using STranslate.Controls;
using STranslate.Plugin;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace STranslate.Tests;

public class SnackbarLifecycleTests
{
    [Fact]
    public void NoticeBar_IsOpenControlsVisibility()
    {
        RunOnStaThread(() =>
        {
            var noticeBar = new NoticeBar();
            Assert.Equal(Visibility.Collapsed, noticeBar.Visibility);

            noticeBar.IsOpen = true;
            Assert.Equal(Visibility.Visible, noticeBar.Visibility);

            noticeBar.IsOpen = false;
            Assert.Equal(Visibility.Collapsed, noticeBar.Visibility);
        });
    }

    [Fact]
    public void SnackbarContainer_DisposeIsIdempotentAndRejectsFurtherUse()
    {
        RunOnStaThread(() =>
        {
            var container = new SnackbarContainer();
            container.Dispose();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                container.Show("message", Severity.Informational, durationMs: 0));
        });
    }

    [Fact]
    public void SnackbarContainer_ShowAndHideAnimationsReachExpectedState()
    {
        RunOnStaThread(() =>
        {
            using var container = new SnackbarContainer();
            var window = new Window
            {
                Width = 1,
                Height = 1,
                Opacity = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = container
            };
            window.Show();
            try
            {
                container.Show("message", Severity.Success, durationMs: 0);
                Assert.Equal(Visibility.Visible, container.Visibility);

                PumpDispatcher(TimeSpan.FromMilliseconds(350));
                container.Hide();
                PumpDispatcher(TimeSpan.FromMilliseconds(300));

                Assert.Equal(Visibility.Collapsed, container.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void NoticeBar_CanBeCollectedAfterUse()
    {
        var weakReference = RunOnStaThread(CreateNoticeBarWeakReference);

        for (int i = 0; i < 3 && weakReference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(weakReference.IsAlive);
    }

    [Fact]
    public void SnackbarContainer_RepeatedShowAndDisposeDoesNotRetainInstances()
    {
        var weakReferences = RunOnStaThread(CreateSnackbarContainerWeakReferences);

        for (int i = 0; i < 3 && weakReferences.Any(x => x.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.All(weakReferences, weakReference => Assert.False(weakReference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateNoticeBarWeakReference()
    {
        var noticeBar = new NoticeBar
        {
            IsOpen = true,
            Message = "message",
            Severity = Severity.Warning
        };
        noticeBar.IsOpen = false;
        return new WeakReference(noticeBar);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateSnackbarContainerWeakReferences()
    {
        var references = new WeakReference[25];
        for (int i = 0; i < references.Length; i++)
        {
            var container = new SnackbarContainer();
            container.Show($"message-{i}", Severity.Warning, durationMs: 0);
            container.Dispose();
            references[i] = new WeakReference(container);
        }
        return references;
    }

    private static void RunOnStaThread(Action action) =>
        RunOnStaThread(() =>
        {
            action();
            return true;
        });

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();

        return result!;
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
