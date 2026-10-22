namespace Game.Core.Content
{
    public sealed class HeroRules
    {
        public HeroRules(int maxLevel, ExponentialCurve experience, double xpPerKill, double bossXpMultiplier, int maxGuardianPoints)
        {
            MaxLevel = maxLevel;
            Experience = experience;
            XpPerKill = xpPerKill;
            BossXpMultiplier = bossXpMultiplier;
            MaxGuardianPoints = maxGuardianPoints;
        }

        public int MaxLevel { get; }
        public ExponentialCurve Experience { get; }
        public double XpPerKill { get; }
        public double BossXpMultiplier { get; }
        public int MaxGuardianPoints { get; }

        public double XpToNext(int level) => Experience.At(level).ToDouble();

        public static HeroRules Read(ContentNode node) =>
            new HeroRules(
                node.Field("maxLevel").AsInt(1, 1000),
                ExponentialCurve.Read(node.Field("experience")),
                node.Field("xpPerKill").AsNumber(0, 1e6),
                node.Field("bossXpMultiplier").AsNumber(1, 1e6),
                node.Field("maxGuardianPoints").AsInt(0, 1000));
    }
}
