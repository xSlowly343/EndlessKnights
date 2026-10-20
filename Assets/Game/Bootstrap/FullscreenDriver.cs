using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class FullscreenDriver : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;

        private WindowController _window = null!;
        private bool _hidden;
        private float _nextCheckTime;

        public static void Attach(GameObject host, WindowController window)
        {
            var driver = host.AddComponent<FullscreenDriver>();
            driver._window = window;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;

            if (!FullscreenDetector.TryGetState(out var state))
                return;
            var hide = FullscreenDetector.ShouldHideStrip(state);
            if (hide == _hidden)
                return;

            _hidden = hide;
            _window.SetHidden(hide);
            RenderPause.Set(hide);
        }
    }
}
