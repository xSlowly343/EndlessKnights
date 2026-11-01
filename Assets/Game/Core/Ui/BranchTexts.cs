using System.Collections.Generic;
using System.Globalization;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class BranchTexts
    {
        public static string Name(SkillBranch branch) => Texts.Pick(branch.NameRu, branch.NameEn);

        public static string Opens(int level) => Texts.Format("branch.opens", level);

        public static string Cost(BigNumber cost, NumberFormatSettings format) => cost.IsZero ? Texts.Get("branch.free") : Texts.Format("branch.cost", NumberFormatter.Format(cost, format));

        public static string Summary(SkillBranch branch)
        {
            var parts = new List<string>(5);
            Add(parts, "branch.damage", branch.DamageMultiplier);
            Add(parts, "branch.cooldown", branch.CooldownMultiplier);
            Add(parts, "branch.costMultiplier", branch.CostMultiplier);
            Add(parts, "branch.duration", branch.DurationMultiplier);
            if (branch.Effect != null)
                parts.Add(Texts.Get("branch.effect"));
            return string.Join(" · ", parts);
        }

        public static string Result(BranchResult result) => result switch
        {
            BranchResult.Done => string.Empty,
            BranchResult.NoSuchBranch => Texts.Get("branch.result.noSuchBranch"),
            BranchResult.LevelTooLow => Texts.Get("branch.result.levelTooLow"),
            BranchResult.SkillBusy => Texts.Get("branch.result.skillBusy"),
            BranchResult.NotEnoughGold => Texts.Get("branch.result.notEnoughGold"),
            _ => string.Empty
        };

        private static void Add(List<string> parts, string key, double multiplier)
        {
            if (System.Math.Abs(multiplier - 1) > 1e-9)
                parts.Add(Texts.Format(key, multiplier.ToString("0.##", CultureInfo.InvariantCulture)));
        }
    }
}
