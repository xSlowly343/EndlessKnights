using Game.Core.Sim;

namespace Game.Core.Ui
{
    public readonly struct ForgeGauge
    {
        public ForgeGauge(double hammers, double heat, double zoneFrom, double zoneTo, bool inZone, double temper, double maxTemper)
        {
            Hammers = hammers;
            Heat = heat;
            ZoneFrom = zoneFrom;
            ZoneTo = zoneTo;
            InZone = inZone;
            Temper = temper;
            MaxTemper = maxTemper;
        }

        public double Hammers { get; }
        public double Heat { get; }
        public double ZoneFrom { get; }
        public double ZoneTo { get; }
        public bool InZone { get; }
        public double Temper { get; }
        public double MaxTemper { get; }
    }

    public static class ForgeView
    {
        public static ForgeGauge Gauge(ForgeSystem forge)
        {
            var rules = forge.Rules;
            var state = forge.State;
            return new ForgeGauge(System.Math.Min(1, state.Hammers / forge.HammersNeeded), state.Heat, forge.ZoneFrom, forge.ZoneTo, forge.InZone(state.Heat),
                forge.TargetMark?.Temper ?? 0, rules.MaxTemper);
        }
    }
}
