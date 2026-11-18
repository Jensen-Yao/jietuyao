using Microsoft.Extensions.Logging;

namespace STranslate.Core;

/// <summary>
/// 后台自动检查更新服务。
/// </summary>
public sealed class AutoUpdateCheckerService(
    ILogger<AutoUpdateCheckerService> logger,
    Settings settings,
    UpdaterService updaterService) : IDisposable
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private readonly CancellationTokenSource _cts = new();
    private Task? _backgroundLoopTask;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// 启动后台自动检查循环。
    /// </summary>
    public void Start()
    {
        if (_disposed || _started)
            return;

        // 开发版本不进行自动检查，避免频繁误报线上更新。
        if (Constant.Version == "1.0.0")
            return;

        _started = true;
        _backgroundLoopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, token);

            while (!token.IsCancellationRequested)
            {
                if (settings.AutoCheckUpdate)
                {
                    await updaterService.NotifyUpdateIfAvailableAsync();
                }

                await Task.Delay(CheckInterval, token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Auto update checker loop canceled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto update checker loop crashed.");
        }
    }

    /// <summary>
    /// 停止后台自动检查循环并释放资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();

        try
        {
            _backgroundLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ignored exception while waiting auto update checker to stop.");
        }

        _cts.Dispose();
    }
}
