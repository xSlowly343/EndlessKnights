using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class FocusModeDriver : MonoBehaviour
    {
        private const float SettleSeconds = 0.5f;

        private readonly FocusMode _focusMode = new FocusMode();
        private WindowController _window = null!;
        private ClickThroughDriver _clickThrough = null!;
        private float _observeAfter;

        public static void Attach(GameObject host, WindowController window, ClickThroughDriver clickThrough)
        {
            var driver = host.AddComponent<FocusModeDriver>();
            driver._window = window;
            driver._clickThrough = clickThrough;
            driver._focusMode.Changed += driver.Apply;
            clickThrough.Changed += driver._focusMode.Set;
            driver._focusMode.Set(clickThrough.Enabled);
        }

        private void Update()
        {
            _window.RememberOtherForeground();
            if (Time.unscaledTime >= _observeAfter)
                _focusMode.Observe(_window.IsForeground);
        }

        private void Apply(bool noFocus)
        {
            _window.SetNoActivate(noFocus);
            if (noFocus)
            {
                _window.ReturnFocusToLastOtherWindow();
                _observeAfter = Time.unscaledTime + SettleSeconds;
            }
            _clickThrough.Enabled = noFocus;
        }
    }
}
