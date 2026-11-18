using System.Threading;

namespace STranslate.Core;

/// <summary>
/// Tracks process-local runtime state that must not be persisted to user settings.
/// </summary>
internal static class AppRuntimeState
{
    private static int _initialSetupInProgress;

    internal static bool IsInitialSetupInProgress => Volatile.Read(ref _initialSetupInProgress) != 0;

    internal static void BeginInitialSetup() => Interlocked.Exchange(ref _initialSetupInProgress, 1);

    internal static void EndInitialSetup() => Interlocked.Exchange(ref _initialSetupInProgress, 0);
}
