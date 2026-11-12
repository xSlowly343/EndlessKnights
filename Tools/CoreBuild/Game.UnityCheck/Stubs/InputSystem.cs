using System;
using UnityEngine;
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl { public bool wasPressedThisFrame; public bool isPressed; public bool wasReleasedThisFrame; }
    public class KeyControl : ButtonControl { public UnityEngine.InputSystem.Key keyCode; }
    public class Vector2Control { public Vector2 ReadValue() => default; }
    public class DeltaControl : Vector2Control {}
}
namespace UnityEngine.InputSystem
{
    using Controls;
    public enum Key { None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0, LeftShift, RightShift, LeftAlt, RightAlt, AltGr, LeftCtrl, RightCtrl, LeftMeta, RightMeta, LeftWindows, RightWindows, LeftApple, RightApple, LeftCommand, RightCommand, ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause, NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals, Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, OEM1, OEM2, OEM3, OEM4, OEM5, IMESelected }
    public class Keyboard { public static Keyboard current; public event Action<char> onTextInput; public System.Collections.ObjectModel.ReadOnlyCollection<KeyControl> allKeys; public KeyControl this[Key key] => null; public KeyControl escapeKey, enterKey, spaceKey, leftShiftKey, rightShiftKey, leftCtrlKey, rightCtrlKey, leftAltKey, rightAltKey, tabKey, backspaceKey; public ButtonControl anyKey; public ButtonControl shiftKey, ctrlKey, altKey; }
    public class InputSettings { public enum BackgroundBehavior { ResetAndDisableNonBackgroundDevices, ResetAndDisableAllDevices, IgnoreFocus } public BackgroundBehavior backgroundBehavior; }
    public static class InputSystem { public static InputSettings settings = new InputSettings(); }
    public class Mouse { public static Mouse current; public DeltaControl delta; public DeltaControl scroll; public ButtonControl leftButton, rightButton, middleButton; public Vector2Control position; }
}
