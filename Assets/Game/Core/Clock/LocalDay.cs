namespace Game.Core.Clock
{
    public static class LocalDay
    {
        public const long MillisecondsPerDay = 24L * 3600 * 1000;

        public static long Of(System.DateTimeOffset localNow) => FromLocalUnixMs(localNow.ToUnixTimeMilliseconds() + (long)localNow.Offset.TotalMilliseconds);

        public static long FromLocalUnixMs(long localUnixMs) =>
            localUnixMs >= 0 ? localUnixMs / MillisecondsPerDay : (localUnixMs - MillisecondsPerDay + 1) / MillisecondsPerDay;
    }
}
