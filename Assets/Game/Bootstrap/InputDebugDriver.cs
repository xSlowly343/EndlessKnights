using Game.Core.Input;
using Game.Platform.Windows.Input;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class InputDebugDriver : MonoBehaviour
    {
        private const float DrainIntervalSeconds = 0.1f;

        private IInputSource _source = null!;
        private InputDebugOverlay _overlay = null!;
        private RawInputService? _rawInput;
        private WindowController? _window;
        private InputTotals _totals;
        private float _nextDrainTime;

        public static void Attach(GameObject host, IInputSource source, InputDebugOverlay overlay, RawInputService? rawInput = null, WindowController? window = null)
        {
            var driver = host.AddComponent<InputDebugDriver>();
            driver._source = source;
            driver._overlay = overlay;
            driver._rawInput = rawInput;
            driver._window = window;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDrainTime)
                return;
            _nextDrainTime = Time.unscaledTime + DrainIntervalSeconds;

            if (_rawInput != null && _window != null && _window.TryGetBounds(out var bounds))
                _rawInput.SetOwnWindowBounds(bounds);

            _totals.Add(_source.Drain());
            _overlay.Show(_totals);
        }
    }
}
