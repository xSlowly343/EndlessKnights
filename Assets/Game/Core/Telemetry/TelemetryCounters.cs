using Game.Core.Input;

namespace Game.Core.Telemetry
{
    public readonly struct TelemetryCounters
    {
        public readonly double Seconds;
        public readonly ulong Keys;
        public readonly ulong Clicks;
        public readonly double MouseMeters;

        public TelemetryCounters(double seconds, ulong keys, ulong clicks, double mouseMeters)
        {
            Seconds = seconds;
            Keys = keys;
            Clicks = clicks;
            MouseMeters = mouseMeters;
        }

        public static TelemetryCounters Of(InputStats stats) => new TelemetryCounters(stats.PlaySeconds, stats.Keys, stats.Clicks, stats.MouseMeters);

        public TelemetryCounters Since(TelemetryCounters start) => new TelemetryCounters(
            System.Math.Max(0, Seconds - start.Seconds),
            Keys >= start.Keys ? Keys - start.Keys : 0,
            Clicks >= start.Clicks ? Clicks - start.Clicks : 0,
            System.Math.Max(0, MouseMeters - start.MouseMeters));

        public double PerMinute(ulong count) => Seconds > 0 ? count * 60.0 / Seconds : 0;
    }
}
