using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class FocusModeDriver : MonoBehaviour
    {
        private readonly FocusMode _focusMode = new FocusMode();
        private WindowController? _window;

        public static void Attach(FocusModeButton button, WindowController? window)
        {
            var driver = button.gameObject.AddComponent<FocusModeDriver>();
            driver._window = window;
            button.Clicked += driver._focusMode.Toggle;
            driver._focusMode.Changed += button.ShowNoFocus;
            driver._focusMode.Changed += driver.ApplyToWindow;
        }

        private void Update()
        {
            if (_window == null)
                return;
            _window.RememberOtherForeground();
            _focusMode.Observe(_window.IsForeground);
        }

        private void ApplyToWindow(bool noFocus)
        {
            if (_window == null)
                return;
            _window.SetNoActivate(noFocus);
            if (noFocus)
                _window.ReturnFocusToLastOtherWindow();
        }
    }
}
