namespace Game.Core.Content
{
    public sealed class StrikeUpgradeRules
    {
        public StrikeUpgradeRules(ExponentialCurve cost, double damagePerLevel, int milestoneEvery, double milestoneMultiplier)
        {
            Cost = cost;
            DamagePerLevel = damagePerLevel;
            MilestoneEvery = milestoneEvery;
            MilestoneMultiplier = milestoneMultiplier;
        }

        public ExponentialCurve Cost { get; }
        public double DamagePerLevel { get; }
        public int MilestoneEvery { get; }
        public double MilestoneMultiplier { get; }

        public double DamageMultiplier(int level) =>
            (1 + DamagePerLevel * level) * System.Math.Pow(MilestoneMultiplier, level / MilestoneEvery);

        public static StrikeUpgradeRules Read(ContentNode node)
        {
            var milestone = node.Field("milestone");
            return new StrikeUpgradeRules(
                ExponentialCurve.Read(node.Field("cost")),
                node.Field("damagePerLevel").AsNumber(0, 1000),
                milestone.Field("every").AsInt(1, 100_000),
                milestone.Field("multiplier").AsNumber(1, 1000));
        }
    }
}
