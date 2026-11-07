namespace Game.Core.Content
{
    public sealed class TempoRules
    {
        public double WindowSeconds { get; private set; } = 60;
        public double From { get; private set; }
        public double Step { get; private set; } = 10;
        public double Cap { get; private set; }
        public double MaxBonus { get; private set; }
        public double RestAfterSeconds { get; private set; }
        public double RestMaxMinutes { get; private set; }
        public int RestHits { get; private set; }

        public double Steps(double perMinute) =>
            System.Math.Floor(System.Math.Max(0, System.Math.Min(Cap, perMinute) - From) / Step + 1e-9);

        internal static TempoRules Read(ContentNode node) => new TempoRules
        {
            WindowSeconds = node.Field("windowSeconds").AsNumber(1, 3600),
            From = node.Field("from").AsNumber(0, 10000),
            Step = node.Field("step").AsNumber(1, 10000),
            Cap = node.Field("cap").AsNumber(0, 10000),
            MaxBonus = node.Field("maxBonus").AsNumber(0, 10),
            RestAfterSeconds = node.Field("rest").Field("afterSeconds").AsNumber(0, 3600),
            RestMaxMinutes = node.Field("rest").Field("maxMinutes").AsNumber(0, 1440),
            RestHits = node.Field("rest").Field("hits").AsInt(0, 100000)
        };
    }
}
