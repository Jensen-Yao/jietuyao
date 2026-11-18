using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;

namespace STranslate.Helpers;

/// <summary>
/// Provides the shared runtime gate for executing global hotkey actions.
/// </summary>
internal static class HotkeyExecutionGuard
{
    internal static bool ShouldSkipGlobalHotkey()
    {
        if (AppRuntimeState.IsInitialSetupInProgress)
            return true;

        try
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();

            if (settings.DisableGlobalHotkeys)
                return true;

            if (settings.IgnoreHotkeysOnFullscreen &&
                Win32Helper.IsForegroundWindowFullscreen())
                return true;
        }
        catch
        {
            // Keep the previous fail-open behavior when settings are temporarily unavailable.
        }

        return false;
    }
}
