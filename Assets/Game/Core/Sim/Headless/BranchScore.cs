using Game.Core.Content.Gems;

namespace Game.Core.Sim.Headless
{
    public static class BranchScore
    {
        public static int Best(SkillBranchTier tier)
        {
            var best = 0;
            for (var option = 1; option < tier.Options.Count; option++)
            {
                if (Of(tier.Options[option]) > Of(tier.Options[best]))
                    best = option;
            }
            return best;
        }

        public static double Of(SkillBranch branch) =>
            branch.DamageMultiplier * branch.DurationMultiplier / (branch.CooldownMultiplier * System.Math.Sqrt(branch.CostMultiplier)) * (branch.Effect != null ? 1.1 : 1);
    }
}
