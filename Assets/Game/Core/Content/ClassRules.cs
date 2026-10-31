namespace Game.Core.Content
{
    public sealed class ClassRules
    {
        public ClassRules(double comboTimeoutSeconds, int comboStrikeEvery, double comboStrikeMultiplier, double whirlResource, double headbangPxPerSecond, double headbangMaxSeconds,
            int breathEvery, int comboStrikeTargets, int comboStrikeEveryMin)
        {
            BreathEvery = breathEvery;
            ComboStrikeTargets = comboStrikeTargets;
            ComboStrikeEveryMin = comboStrikeEveryMin;
            HeadbangPxPerSecond = headbangPxPerSecond;
            HeadbangMaxSeconds = headbangMaxSeconds;
            WhirlResource = whirlResource;
            ComboTimeoutSeconds = comboTimeoutSeconds;
            ComboStrikeEvery = comboStrikeEvery;
            ComboStrikeMultiplier = comboStrikeMultiplier;
        }

        public double ComboTimeoutSeconds { get; }
        public int ComboStrikeEvery { get; }
        public double ComboStrikeMultiplier { get; }
        public double WhirlResource { get; }
        public int BreathEvery { get; }
        public int ComboStrikeTargets { get; }
        public int ComboStrikeEveryMin { get; }
        public double HeadbangPxPerSecond { get; }
        public double HeadbangMaxSeconds { get; }

        public static ClassRules Read(ContentNode node)
        {
            var combo = node.Field("ronin").Field("combo");
            var headbang = node.Field("metalhead").Field("headbang");
            return new ClassRules(
                combo.Field("timeoutSeconds").AsNumber(0.1, 60),
                combo.Field("strikeEvery").AsInt(1, 10000),
                combo.Field("strikeMultiplier").AsNumber(1, 1000),
                combo.Field("whirlResource").AsNumber(0, 1000),
                headbang.Field("pxPerSecond").AsNumber(1, 1e7),
                headbang.Field("maxSeconds").AsNumber(0, 3600),
                combo.Field("breathEvery").AsInt(1, 10000),
                combo.Field("strikeTargets").AsInt(0, 100),
                combo.Field("strikeEveryMin").AsInt(1, 10000));
        }
    }
}
