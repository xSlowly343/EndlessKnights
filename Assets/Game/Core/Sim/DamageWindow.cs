using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class DamageWindow
    {
        public const int Seconds = 10;
        public const int Capacity = Seconds * GameLoop.TicksPerSecond + 1;

        private readonly BigNumber[] _totals = new BigNumber[Capacity];
        private int _next;
        private int _count;

        public void Record(BigNumber total)
        {
            _totals[_next] = total;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
                _count++;
        }

        public BigNumber Last(BigNumber total, long ticks)
        {
            var back = (int)System.Math.Max(1, ticks);
            if (back >= _count)
                return total;
            return total - _totals[(_next - back - 1 + Capacity) % Capacity];
        }
    }
}
