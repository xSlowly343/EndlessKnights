namespace Game.Core.Clock
{
    public sealed class TrustedDay
    {
        private readonly long _anchorLocalMs;
        private readonly ClockReading _anchor;

        private TrustedDay(long anchorLocalMs, ClockReading anchor)
        {
            _anchorLocalMs = anchorLocalMs;
            _anchor = anchor;
        }

        public static TrustedDay Start(ClockStamp? saved, in ClockReading now, long utcOffsetMs)
        {
            if (saved == null)
                return new TrustedDay(now.WallUnixMs + utcOffsetMs, now);
            var stamp = saved.Value;
            var elapsed = TrustedElapsed.Between(stamp, now);
            var trustedWall = stamp.Reading.WallUnixMs + (long)System.Math.Round(elapsed.Seconds * 1000);
            return new TrustedDay(trustedWall + utcOffsetMs, now);
        }

        public long Today(in ClockReading now)
        {
            var sameBoot = now.BootId == _anchor.BootId && now.MonotonicMs >= _anchor.MonotonicMs;
            var since = sameBoot ? now.MonotonicMs - _anchor.MonotonicMs : System.Math.Max(0, now.WallUnixMs - _anchor.WallUnixMs);
            return LocalDay.FromLocalUnixMs(_anchorLocalMs + since);
        }
    }
}
