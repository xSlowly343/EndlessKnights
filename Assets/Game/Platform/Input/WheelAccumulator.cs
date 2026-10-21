using System;

namespace Game.Platform.Windows.Input
{
    public sealed class WheelAccumulator
    {
        public const int NotchDelta = 120;

        private int _remainder;

        public int AddDelta(int delta)
        {
            _remainder += Math.Abs(delta);
            var ticks = _remainder / NotchDelta;
            _remainder -= ticks * NotchDelta;
            return ticks;
        }
    }
}
