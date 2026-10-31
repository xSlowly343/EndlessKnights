namespace Game.Core.Content.Gems
{
    public sealed class GemLevels
    {
        public GemLevels(int max, double effectPerLevel, ExponentialCurve experience, double experiencePerKill, double experiencePerCast = 0)
        {
            ExperiencePerCast = experiencePerCast;
            ExperiencePerKill = experiencePerKill;
            Max = max;
            EffectPerLevel = effectPerLevel;
            Experience = experience;
        }

        public int Max { get; }
        public double EffectPerLevel { get; }
        public ExponentialCurve Experience { get; }
        public double ExperiencePerKill { get; }
        public double ExperiencePerCast { get; }

        public double ToNext(int level) => Experience.At(level).ToDouble();

        public double EffectMultiplier(int level) => 1 + EffectPerLevel * (System.Math.Max(1, System.Math.Min(level, Max)) - 1);

        public double EffectMultiplier(int level, int bonus) => EffectMultiplier(level) + EffectPerLevel * System.Math.Max(0, bonus);

        public static GemLevels Read(ContentNode node) =>
            new GemLevels(
                node.Field("max").AsInt(1, 100),
                node.Field("effectPerLevel").AsNumber(0, 10),
                ExponentialCurve.Read(node.Field("experience")),
                node.Field("experiencePerKill").AsNumber(0, 1e9),
                node.Field("experiencePerCast").AsNumber(0, 1e9));
    }
}
