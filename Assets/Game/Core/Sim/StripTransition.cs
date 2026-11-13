using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum TransitionPhase
    {
        None,
        Open,
        Enter,
        Swap,
        Arrive,
        Exit
    }

    public sealed class StripTransition
    {
        private const int PhaseCount = 5;

        private readonly int[] _ticks = new int[PhaseCount];
        private PortalRule _rule = PortalRule.None;
        private int _phase = PhaseCount;
        private int _age;
        private int _minLoadTicks;
        private int _maxLoadTicks;

        public StripPlace From { get; private set; }
        public StripPlace To { get; private set; }
        public bool Group => _rule.Group;
        public bool Loads => _rule.Loads;
        public bool Loaded { get; private set; }
        public bool Active => _phase < PhaseCount;
        public TransitionPhase Phase => Active ? (TransitionPhase)(_phase + 1) : TransitionPhase.None;
        public StripPlace Shown => _phase <= (int)TransitionPhase.Enter - 1 || (_phase == (int)TransitionPhase.Swap - 1 && !PastHalf) ? From : To;

        public double PhaseProgress
        {
            get
            {
                if (!Active)
                    return 1;
                var length = PhaseLength;
                return length <= 0 ? 1 : System.Math.Min(1, (double)_age / length);
            }
        }

        private bool PastHalf => _age * 2 >= PhaseLength;

        private int PhaseLength => Loads && _phase == (int)TransitionPhase.Swap - 1 ? System.Math.Max(_ticks[_phase], _minLoadTicks) : _ticks[_phase];

        public void Start(StripPlace from, StripPlace to, PortalRule rule)
        {
            From = from;
            To = to;
            _rule = rule;
            _ticks[0] = Ticks(rule.Open);
            _ticks[1] = Ticks(rule.Enter);
            _ticks[2] = Ticks(rule.Swap);
            _ticks[3] = Ticks(rule.Arrive);
            _ticks[4] = Ticks(rule.Exit);
            _minLoadTicks = Ticks(rule.MinLoad);
            _maxLoadTicks = Ticks(rule.MaxLoad);
            Loaded = !rule.Loads;
            _phase = 0;
            _age = 0;
            Skip();
        }

        public void MarkLoaded() => Loaded = true;

        public void Tick()
        {
            if (!Active)
                return;
            _age++;
            if (_age < PhaseLength || Waiting)
                return;
            _phase++;
            _age = 0;
            Skip();
        }

        private bool Waiting => Loads && _phase == (int)TransitionPhase.Swap - 1 && !Loaded && _age < _maxLoadTicks;

        private void Skip()
        {
            while (Active && PhaseLength == 0 && !Waiting)
                _phase++;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
