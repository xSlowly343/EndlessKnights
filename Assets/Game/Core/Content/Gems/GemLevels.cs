namespace Game.Core.Content.Gems
{
    public sealed class GemLevels
    {
        public GemLevels(int max, double effectPerLevel, ExponentialCurve experience)
        {
            Max = max;
            EffectPerLevel = effectPerLevel;
            Experience = experience;
        }

        public int Max { get; }
        public double EffectPerLevel { get; }
        public ExponentialCurve Experience { get; }

        public double EffectMultiplier(int level) => 1 + EffectPerLevel * (System.Math.Max(1, System.Math.Min(level, Max)) - 1);

        public static GemLevels Read(ContentNode node) =>
            new GemLevels(
                node.Field("max").AsInt(1, 100),
                node.Field("effectPerLevel").AsNumber(0, 10),
                ExponentialCurve.Read(node.Field("experience")));
    }
}
