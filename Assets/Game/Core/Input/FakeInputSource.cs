namespace Game.Core.Input
{
    public sealed class FakeInputSource : IInputSource
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
        private int _flicks;
        private float _circleTurns;
        private int _comboDowns;

        public double SecondsSinceLastInput { get; set; }
        public float RhythmCv { get; set; }
        public bool Suppressed { get; set; }

        public void PressKeys(int count) => _keyDowns += count;

        public void PressEnter(int count = 1)
        {
            _keyDowns += count;
            _enterDowns += count;
        }

        public void PressBackspace(int count = 1)
        {
            _keyDowns += count;
            _backspaceDowns += count;
        }

        public void PressSpace(int count = 1)
        {
            _keyDowns += count;
            _spaceDowns += count;
        }

        public void PressCombo(int count = 1)
        {
            _keyDowns += count;
            _comboDowns += count;
        }

        public void ClickLeft(int count = 1) => _leftClicks += count;
        public void ClickRight(int count = 1) => _rightClicks += count;
        public void ClickMiddle(int count = 1) => _middleClicks += count;
        public void ScrollWheel(int ticks) => _wheelTicks += ticks;
        public void MoveMouse(float distancePx) => _mouseDistancePx += distancePx;
        public void Flick(int count = 1) => _flicks += count;
        public void Circle(float turns) => _circleTurns += turns;

        public InputSnapshot Drain()
        {
            var snapshot = new InputSnapshot(
                _keyDowns, _enterDowns, _backspaceDowns, _spaceDowns,
                _leftClicks, _rightClicks, _middleClicks, _wheelTicks,
                _mouseDistancePx, _flicks, _circleTurns,
                SecondsSinceLastInput, RhythmCv, Suppressed, _comboDowns);

            _keyDowns = 0;
            _enterDowns = 0;
            _backspaceDowns = 0;
            _spaceDowns = 0;
            _leftClicks = 0;
            _rightClicks = 0;
            _middleClicks = 0;
            _wheelTicks = 0;
            _mouseDistancePx = 0f;
            _flicks = 0;
            _circleTurns = 0f;
            _comboDowns = 0;
            return snapshot;
        }
    }
}
