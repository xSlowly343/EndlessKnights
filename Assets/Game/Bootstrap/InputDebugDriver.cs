using Game.Core.Input;
using Game.Platform.Windows.Input;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class InputDebugDriver : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.1f;

        private InputDebugOverlay _overlay = null!;
        private RawInputService? _rawInput;
        private WindowController? _window;
        private InputTotals _totals;
        private float _nextRefreshTime;

        public static void Attach(GameObject host, ObservedInputSource source, InputDebugOverlay overlay, RawInputService? rawInput = null, WindowController? window = null)
        {
            var driver = host.AddComponent<InputDebugDriver>();
            driver._overlay = overlay;
            driver._rawInput = rawInput;
            driver._window = window;
            source.Drained += snapshot => driver._totals.Add(snapshot);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;
            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;

            if (_rawInput != null && _window != null && _window.TryGetBounds(out var bounds))
                _rawInput.SetOwnWindowBounds(bounds);

            _overlay.Show(_totals);
        }
    }
}
