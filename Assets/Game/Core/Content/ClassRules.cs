namespace Game.Core.Content
{
    public sealed class ClassRules
    {
        public ClassRules(double comboTimeoutSeconds, int comboStrikeEvery, double comboStrikeMultiplier, double whirlResource)
        {
            WhirlResource = whirlResource;
            ComboTimeoutSeconds = comboTimeoutSeconds;
            ComboStrikeEvery = comboStrikeEvery;
            ComboStrikeMultiplier = comboStrikeMultiplier;
        }

        public double ComboTimeoutSeconds { get; }
        public int ComboStrikeEvery { get; }
        public double ComboStrikeMultiplier { get; }
        public double WhirlResource { get; }

        public static ClassRules Read(ContentNode node)
        {
            var combo = node.Field("ronin").Field("combo");
            return new ClassRules(
                combo.Field("timeoutSeconds").AsNumber(0.1, 60),
                combo.Field("strikeEvery").AsInt(1, 10000),
                combo.Field("strikeMultiplier").AsNumber(1, 1000),
                combo.Field("whirlResource").AsNumber(0, 1000));
        }
    }
}
