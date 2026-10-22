namespace Game.Core.Clock
{
    public readonly struct ClockStamp
    {
        public readonly ClockReading Reading;
        public readonly long HighestWallUnixMs;

        public ClockStamp(ClockReading reading, long highestWallUnixMs)
        {
            Reading = reading;
            HighestWallUnixMs = highestWallUnixMs;
        }

        public static ClockStamp First(ClockReading now) => new ClockStamp(now, now.WallUnixMs);

        public ClockStamp Next(ClockReading now) => new ClockStamp(now, System.Math.Max(HighestWallUnixMs, now.WallUnixMs));
    }
}
