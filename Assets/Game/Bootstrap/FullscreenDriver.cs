using System;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class FullscreenDriver : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;

        private WindowController _window = null!;
        private StripVisibility _visibility;
        private bool _hidden;
        private float _nextCheckTime;

        public event Action<bool>? HiddenByHandChanged;
        public event Action? Shown;

        public bool HiddenByHand => _visibility.ByHand;
        public bool Hidden => _hidden;

        public static FullscreenDriver Attach(GameObject host, WindowController window)
        {
            var driver = host.AddComponent<FullscreenDriver>();
            driver._window = window;
            return driver;
        }

        public void UseAutoHide(bool enabled)
        {
            _visibility.AutoHide = enabled;
            if (!enabled)
                _visibility.Covered = false;
            _nextCheckTime = 0f;
            Apply();
        }

        public void ToggleHidden() => SetHiddenByHand(!_visibility.ByHand);

        public void SetHiddenByHand(bool hidden)
        {
            if (_visibility.ByHand == hidden)
                return;
            _visibility.ByHand = hidden;
            Apply();
            HiddenByHandChanged?.Invoke(hidden);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;

            if (FullscreenDetector.TryGetState(out var state))
                _visibility.Exclusive = FullscreenDetector.ShouldHideStrip(state);
            _visibility.Covered = _visibility.AutoHide && _window.ForegroundCoversOwnMonitor();
            Apply();
        }

        private void Apply()
        {
            var hide = _visibility.Hidden;
            if (hide == _hidden)
                return;
            _hidden = hide;
            _window.SetHidden(hide);
            RenderPause.Set(RenderPauseReason.Fullscreen, hide);
            if (!hide)
                Shown?.Invoke();
        }
    }
}
