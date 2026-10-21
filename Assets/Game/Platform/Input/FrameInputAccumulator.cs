using Game.Core.Input;

namespace Game.Platform.Windows.Input
{
    public sealed class FrameInputAccumulator
    {
        private int _keyDowns;
        private int _enterDowns;
        private int _backspaceDowns;
        private int _spaceDowns;
        private int _leftClicks;
        private int _rightClicks;
        private int _middleClicks;
        private int _wheelTicks;
        private float _mouseDistancePx;
        private double _lastInputSeconds = double.NaN;

        public void AddKey(KeyClass keyClass, double nowSeconds)
        {
            if (keyClass == KeyClass.None)
                return;
            _keyDowns++;
            if (keyClass == KeyClass.Enter)
                _enterDowns++;
            else if (keyClass == KeyClass.Backspace)
                _backspaceDowns++;
            else if (keyClass == KeyClass.Space)
                _spaceDowns++;
            _lastInputSeconds = nowSeconds;
        }

        public void AddClicks(int left, int right, int middle, double nowSeconds)
        {
            if (left + right + middle == 0)
                return;
            _leftClicks += left;
            _rightClicks += right;
            _middleClicks += middle;
            _lastInputSeconds = nowSeconds;
        }

        public void AddWheelTicks(int ticks, double nowSeconds)
        {
            if (ticks <= 0)
                return;
            _wheelTicks += ticks;
            _lastInputSeconds = nowSeconds;
        }

        public void AddMouseDistance(float distancePx, double nowSeconds)
        {
            if (distancePx <= 0f)
                return;
            _mouseDistancePx += distancePx;
            _lastInputSeconds = nowSeconds;
        }

        public InputSnapshot Drain(double nowSeconds)
        {
            var snapshot = new InputSnapshot(
                _keyDowns, _enterDowns, _backspaceDowns, _spaceDowns,
                _leftClicks, _rightClicks, _middleClicks, _wheelTicks, _mouseDistancePx,
                secondsSinceLastInput: double.IsNaN(_lastInputSeconds) ? 0d : nowSeconds - _lastInputSeconds);
            _keyDowns = 0;
            _enterDowns = 0;
            _backspaceDowns = 0;
            _spaceDowns = 0;
            _leftClicks = 0;
            _rightClicks = 0;
            _middleClicks = 0;
            _wheelTicks = 0;
            _mouseDistancePx = 0f;
            return snapshot;
        }
    }
}
