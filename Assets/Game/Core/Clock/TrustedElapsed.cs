namespace Game.Core.Clock
{
    public readonly struct TrustedElapsed
    {
        public const long ToleranceMs = 60_000;

        public readonly double Seconds;
        public readonly ElapsedSource Source;
        public readonly bool ClockTampered;

        private TrustedElapsed(long milliseconds, ElapsedSource source, bool clockTampered)
        {
            Seconds = System.Math.Max(0, milliseconds) / 1000.0;
            Source = source;
            ClockTampered = clockTampered;
        }

        public static TrustedElapsed Between(in ClockStamp saved, in ClockReading now)
        {
            var before = saved.Reading;
            if (before.ServerUnixMs.HasValue && now.ServerUnixMs.HasValue)
            {
                var server = now.ServerUnixMs.Value - before.ServerUnixMs.Value;
                var wall = now.WallUnixMs - before.WallUnixMs;
                return new TrustedElapsed(server, ElapsedSource.Server, System.Math.Abs(wall - server) > ToleranceMs);
            }

            if (before.BootId == now.BootId && now.MonotonicMs >= before.MonotonicMs)
            {
                var monotonic = now.MonotonicMs - before.MonotonicMs;
                var wall = now.WallUnixMs - before.WallUnixMs;
                return new TrustedElapsed(monotonic, ElapsedSource.Monotonic, System.Math.Abs(wall - monotonic) > ToleranceMs);
            }

            var sinceHighest = now.WallUnixMs - saved.HighestWallUnixMs;
            var movedBack = now.WallUnixMs < before.WallUnixMs - ToleranceMs || saved.HighestWallUnixMs > before.WallUnixMs + ToleranceMs;
            return new TrustedElapsed(sinceHighest, ElapsedSource.Wall, movedBack);
        }
    }
}
