namespace Game.Core.Ui
{
    public sealed class AllySlashTurns
    {
        public const double DefaultGapSeconds = 0.2;

        private readonly SlashGate[] _gates;
        private readonly double _gapSeconds;
        private double _nextAt = double.NegativeInfinity;
        private int _last = -1;

        public AllySlashTurns(int allies, double gapSeconds = DefaultGapSeconds)
        {
            _gapSeconds = gapSeconds;
            _gates = new SlashGate[allies];
            for (var i = 0; i < allies; i++)
                _gates[i] = new SlashGate();
        }

        public int Pending(int ally) => _gates[ally].Pending;

        public void Hit(int ally, int hits) => _gates[ally].Hit(hits);

        public void Clear(int ally) => _gates[ally].Take();

        public bool TryTake(double now, bool[] ready, out int ally, out int tier)
        {
            ally = -1;
            tier = 0;
            if (now < _nextAt)
                return false;
            for (var step = 1; step <= _gates.Length; step++)
            {
                var index = (_last + step + _gates.Length) % _gates.Length;
                if (_gates[index].Pending == 0 || !ready[index])
                    continue;
                ally = index;
                tier = _gates[index].Take();
                _last = index;
                _nextAt = now + _gapSeconds;
                return true;
            }
            return false;
        }
    }
}
