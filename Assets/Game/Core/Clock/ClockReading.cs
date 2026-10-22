namespace Game.Core.Clock
{
    public readonly struct ClockReading
    {
        public readonly long WallUnixMs;
        public readonly long MonotonicMs;
        public readonly long BootId;
        public readonly long? ServerUnixMs;

        public ClockReading(long wallUnixMs, long monotonicMs, long bootId, long? serverUnixMs = null)
        {
            WallUnixMs = wallUnixMs;
            MonotonicMs = monotonicMs;
            BootId = bootId;
            ServerUnixMs = serverUnixMs;
        }
    }
}
