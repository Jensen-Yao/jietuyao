namespace STranslate.Core;

public sealed class DebounceExecutor : IDisposable
{
    private readonly Lock _lock = new();
    private long _generation;
    private bool _disposed;

    /// <summary>
    /// 取消当前待执行的防抖任务
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _generation++;
        }
    }

    /// <summary>
    /// 同步动作防抖执行
    /// </summary>
    public void Execute(Action action, TimeSpan delay)
    {
        var generation = NextGeneration(); // 内部已检查 _disposed
        _ = RunAfterDelayAsync(generation, delay, action);
    }

    /// <summary>
    /// 异步动作防抖执行 (支持 async/await)
    /// </summary>
    public void ExecuteAsync(Func<Task> asyncAction, TimeSpan delay)
    {
        var generation = NextGeneration();
        _ = RunAfterDelayAsync(generation, delay, asyncAction);
    }

    private long NextGeneration()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _generation++;
            return _generation;
        }
    }

    private bool IsLatestGeneration(long generation)
    {
        lock (_lock)
        {
            return !_disposed && generation == _generation;
        }
    }

    private async Task RunAfterDelayAsync(long generation, TimeSpan delay, Action action)
    {
        try
        {
            await Task.Delay(delay).ConfigureAwait(false);

            if (!IsLatestGeneration(generation))
                return;

            action.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebounceExecutor] Action threw: {ex}");
        }
    }

    private async Task RunAfterDelayAsync(long generation, TimeSpan delay, Func<Task> asyncAction)
    {
        try
        {
            await Task.Delay(delay).ConfigureAwait(false);

            if (!IsLatestGeneration(generation))
                return;

            await asyncAction().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebounceExecutor] AsyncAction threw: {ex}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _generation++;
        }
    }
}