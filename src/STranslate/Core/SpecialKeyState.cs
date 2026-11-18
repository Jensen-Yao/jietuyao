using System.Windows.Input;
using Windows.Win32;

namespace STranslate.Core;

// <summary>
/// Contains the press state of certain special keys.
/// </summary>
public class SpecialKeyState
{
    /// <summary>
    /// True if the Ctrl key is pressed.
    /// </summary>
    public bool CtrlPressed { get; set; }

    /// <summary>
    /// True if the Shift key is pressed.
    /// </summary>
    public bool ShiftPressed { get; set; }

    /// <summary>
    /// True if the Alt key is pressed.
    /// </summary>
    public bool AltPressed { get; set; }

    /// <summary>
    /// True if the Windows key is pressed.
    /// </summary>
    public bool WinPressed { get; set; }

    /// <summary>
    /// Get this object represented as a <see cref="ModifierKeys"/> flag combination.
    /// </summary>
    /// <returns></returns>
    public ModifierKeys ToModifierKeys() => (CtrlPressed ? ModifierKeys.Control : ModifierKeys.None) |
               (ShiftPressed ? ModifierKeys.Shift : ModifierKeys.None) |
               (AltPressed ? ModifierKeys.Alt : ModifierKeys.None) |
               (WinPressed ? ModifierKeys.Windows : ModifierKeys.None);

    /// <summary>
    /// Default <see cref="SpecialKeyState"/> object with all keys not pressed.
    /// </summary>
    public static readonly SpecialKeyState Default = new()
    {
        CtrlPressed = false,
        ShiftPressed = false,
        AltPressed = false,
        WinPressed = false
    };
}


/// <summary>
/// Enumeration of key events for 
/// <see cref="IPublicAPI.RegisterGlobalKeyboardCallback(Func{int, int, SpecialKeyState, bool})"/>
/// and <see cref="IPublicAPI.RemoveGlobalKeyboardCallback(Func{int, int, SpecialKeyState, bool})"/>
/// </summary>
public enum KeyEvent
{
    /// <summary>
    /// Key down
    /// </summary>
    WM_KEYDOWN = (int)PInvoke.WM_KEYDOWN,

    /// <summary>
    /// Key up
    /// </summary>
    WM_KEYUP = (int)PInvoke.WM_KEYUP,

    /// <summary>
    /// System key up
    /// </summary>
    WM_SYSKEYUP = (int)PInvoke.WM_SYSKEYUP,

    /// <summary>
    /// System key down
    /// </summary>
    WM_SYSKEYDOWN = (int)PInvoke.WM_SYSKEYDOWN
}