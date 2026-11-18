using STranslate.Helpers;

namespace STranslate.Tests;

public class WindowActivationContextTests
{
    [Fact]
    public void DefaultsToNormal()
    {
        Assert.Equal(WindowActivationMode.Normal, WindowActivationContext.Current);
    }

    [Fact]
    public void PushRestoresPreviousModeWhenDisposed()
    {
        using (WindowActivationContext.Push(WindowActivationMode.ForceForeground))
        {
            Assert.Equal(WindowActivationMode.ForceForeground, WindowActivationContext.Current);
        }

        Assert.Equal(WindowActivationMode.Normal, WindowActivationContext.Current);
    }

    [Fact]
    public void NestedScopeRestoresOuterMode()
    {
        using (WindowActivationContext.Push(WindowActivationMode.ForceForeground))
        {
            using (WindowActivationContext.Push(WindowActivationMode.Normal))
            {
                Assert.Equal(WindowActivationMode.Normal, WindowActivationContext.Current);
            }

            Assert.Equal(WindowActivationMode.ForceForeground, WindowActivationContext.Current);
        }
    }

    [Fact]
    public void ScopeRestoresModeAfterException()
    {
        Action action = () =>
        {
            using var _ = WindowActivationContext.Push(WindowActivationMode.ForceForeground);
            throw new InvalidOperationException("boom");
        };

        Assert.Throws<InvalidOperationException>(action);

        Assert.Equal(WindowActivationMode.Normal, WindowActivationContext.Current);
    }

    [Fact]
    public async Task ModeFlowsAcrossAwait()
    {
        using var _ = WindowActivationContext.Push(WindowActivationMode.ForceForeground);

        await Task.Yield();

        Assert.Equal(WindowActivationMode.ForceForeground, WindowActivationContext.Current);
    }

    [Fact]
    public async Task ConcurrentAsyncFlowsRemainIsolated()
    {
        var forceScopeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var normalFlowObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var forceFlow = Task.Run(async () =>
        {
            using var _ = WindowActivationContext.Push(WindowActivationMode.ForceForeground);
            forceScopeEntered.SetResult();
            await normalFlowObserved.Task;
            return WindowActivationContext.Current;
        });

        var normalFlow = Task.Run(async () =>
        {
            await forceScopeEntered.Task;
            var mode = WindowActivationContext.Current;
            normalFlowObserved.SetResult();
            return mode;
        });

        var results = await Task.WhenAll(forceFlow, normalFlow);

        Assert.Equal(WindowActivationMode.ForceForeground, results[0]);
        Assert.Equal(WindowActivationMode.Normal, results[1]);
        Assert.Equal(WindowActivationMode.Normal, WindowActivationContext.Current);
    }

    [Fact]
    public void SelectUsesActionForCurrentMode()
    {
        var normalResult = WindowActivationContext.Select(
            normal: () => "normal",
            forceForeground: () => "force");

        using var _ = WindowActivationContext.Push(WindowActivationMode.ForceForeground);
        var forceResult = WindowActivationContext.Select(
            normal: () => "normal",
            forceForeground: () => "force");

        Assert.Equal("normal", normalResult);
        Assert.Equal("force", forceResult);
    }
}
