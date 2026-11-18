using STranslate.Views;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;

namespace STranslate.Tests;

public class MouseSelectionIconWindowTests
{
    [Fact]
    public void IconContentStartsHiddenWhileWindowRemainsOpaque()
    {
        RunOnStaThread(() =>
        {
            var window = new MouseSelectionIconWindow();
            Assert.Equal(1d, window.Opacity);
            Assert.Equal(0d, window.IconRoot.Opacity);
        });
    }

    [Fact]
    public void ShowAnimationTargetsIconContentInsteadOfWindow()
    {
        RunOnStaThread(() =>
        {
            var window = new MouseSelectionIconWindow();
            window.StartShowAnimation();

            Assert.False(window.HasAnimatedProperties);
            Assert.True(window.IconRoot.HasAnimatedProperties);
            Assert.Equal(0d, window.IconRoot.GetAnimationBaseValue(UIElement.OpacityProperty));
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
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
    }
}
