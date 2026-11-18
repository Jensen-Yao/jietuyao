namespace STranslate.Helpers;

internal enum WindowActivationMode
{
    Normal,
    ForceForeground
}

internal static class WindowActivationContext
{
    private static readonly AsyncLocal<WindowActivationMode?> CurrentMode = new();

    public static WindowActivationMode Current => CurrentMode.Value ?? WindowActivationMode.Normal;

    public static IDisposable Push(WindowActivationMode mode)
    {
        var previousMode = CurrentMode.Value;
        CurrentMode.Value = mode;
        return new Scope(previousMode);
    }

    public static T Select<T>(Func<T> normal, Func<T> forceForeground)
        => Current == WindowActivationMode.ForceForeground
            ? forceForeground()
            : normal();

    private sealed class Scope(WindowActivationMode? previousMode) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            CurrentMode.Value = previousMode;
            _disposed = true;
        }
    }
}
