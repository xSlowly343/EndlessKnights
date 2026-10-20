using Game.Core.Input;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class InputDebugDriver : MonoBehaviour
    {
        private const float DrainIntervalSeconds = 0.1f;

        private IInputSource _source = null!;
        private InputDebugOverlay _overlay = null!;
        private int _totalKeyDowns;
        private int _enterDowns;
        private int _backspaceDowns;
        private int _spaceDowns;
        private float _nextDrainTime;

        public static void Attach(GameObject host, IInputSource source, InputDebugOverlay overlay)
        {
            var driver = host.AddComponent<InputDebugDriver>();
            driver._source = source;
            driver._overlay = overlay;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDrainTime)
                return;
            _nextDrainTime = Time.unscaledTime + DrainIntervalSeconds;

            var snapshot = _source.Drain();
            _totalKeyDowns += snapshot.KeyDowns;
            _enterDowns += snapshot.EnterDowns;
            _backspaceDowns += snapshot.BackspaceDowns;
            _spaceDowns += snapshot.SpaceDowns;
            _overlay.Show(_totalKeyDowns, _enterDowns, _backspaceDowns, _spaceDowns);
        }
    }
}
