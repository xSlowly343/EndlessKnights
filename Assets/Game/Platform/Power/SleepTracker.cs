using System;

namespace Game.Platform.Windows.Power
{
    public sealed class SleepTracker
    {
        private DateTime? _suspendedAtUtc;

        public void Suspended(DateTime utcNow)
        {
            _suspendedAtUtc = utcNow;
        }

        public TimeSpan Resumed(DateTime utcNow)
        {
            if (_suspendedAtUtc == null)
                return TimeSpan.Zero;
            var asleep = utcNow - _suspendedAtUtc.Value;
            _suspendedAtUtc = null;
            return asleep > TimeSpan.Zero ? asleep : TimeSpan.Zero;
        }
    }
}
