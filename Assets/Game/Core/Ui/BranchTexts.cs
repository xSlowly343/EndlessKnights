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

        public static string Evolves(GemDef next, int heroLevel) => Texts.Format("skill.evolves", heroLevel, Texts.Pick(next.NameRu, next.NameEn));

        public static string Cost(BigNumber cost, NumberFormatSettings format) => cost.IsZero ? Texts.Get("branch.free") : Texts.Format("branch.cost", NumberFormatter.Format(cost, format));

        public static string Summary(SkillBranch branch, SkillEffect? skillEffect = null)
        {
            var parts = new List<string>(8);
            Add(parts, "branch.damage", branch.DamageMultiplier);
            Add(parts, "branch.cooldown", branch.CooldownMultiplier);
            Add(parts, "branch.costMultiplier", branch.CostMultiplier);
            Add(parts, "branch.duration", branch.DurationMultiplier);
            if (branch.Effect != null && !Rules(parts, branch.Effect, skillEffect))
                parts.Add(Texts.Get("branch.effect"));
            return string.Join(" · ", parts);
        }

        private static bool Rules(List<string> parts, SkillEffect branch, SkillEffect? skill)
        {
            var before = parts.Count;
            if (branch is StrikeEffect strike && skill is StrikeEffect plain)
                StrikeRules(parts, strike, plain);
            else if (branch is FlurryEffect flurry && skill is FlurryEffect plainFlurry)
                FlurryRules(parts, flurry, plainFlurry);
            else if (branch is AuraEffect aura && skill is AuraEffect plainAura && aura.Reserve != plainAura.Reserve)
                parts.Add(Texts.Format("branch.rule.reserve", Number(aura.Reserve)));
            return parts.Count > before;
        }

        private static void StrikeRules(List<string> parts, StrikeEffect strike, StrikeEffect plain)
        {
            if (strike.BossMultiplier != plain.BossMultiplier && strike.BossMultiplier > 0)
                parts.Add(Texts.Format("branch.rule.boss", Number(strike.BossMultiplier)));
            if (strike.PauseSeconds != plain.PauseSeconds && strike.PauseSeconds > 0)
                parts.Add(Texts.Format("branch.rule.pause", Number(strike.PauseSeconds), Number(strike.PauseMultiplier)));
            if (strike.LowHpBelow != plain.LowHpBelow && strike.LowHpBelow > 0)
                parts.Add(Texts.Format("branch.rule.lowHp", Percent(strike.LowHpBelow), Number(strike.LowHpMultiplier)));
            if (strike.ExecuteBelow != plain.ExecuteBelow && strike.ExecuteBelow > 0)
                parts.Add(Texts.Format("branch.rule.execute", Percent(strike.ExecuteBelow)));
            if (strike.KillRefundShare != plain.KillRefundShare && strike.KillRefundShare > 0)
                parts.Add(Texts.Format("branch.rule.refund", Percent(strike.KillRefundShare)));
            if (strike.BossTimerStopSeconds != plain.BossTimerStopSeconds && strike.BossTimerStopSeconds > 0)
                parts.Add(Texts.Format("branch.rule.timerStop", Number(strike.BossTimerStopSeconds)));
            if (strike.QueueTargets != plain.QueueTargets)
                parts.Add(strike.QueueTargets > 0 ? Texts.Format("branch.rule.queue", strike.QueueTargets) : Texts.Get("branch.rule.noQueue"));
            if (strike.CritChance != plain.CritChance && strike.CritChance > plain.CritChance)
                parts.Add(Texts.Format("branch.rule.crit", Percent(strike.CritChance - plain.CritChance)));
            if (strike.Multiplier != plain.Multiplier)
                parts.Add(Texts.Format("branch.rule.multiplier", Number(strike.Multiplier)));
            if (strike.Runes != plain.Runes)
                parts.Add(Texts.Format("branch.rule.runes", strike.Runes));
            if (strike.Blades != plain.Blades)
                parts.Add(Texts.Format("branch.rule.blades", strike.Blades));
            if (strike.Touches != plain.Touches)
                parts.Add(Texts.Format("branch.rule.touches", strike.Touches));
            if (strike.Word && !plain.Word)
                parts.Add(Texts.Get("branch.rule.word"));
            if (strike.PhantomSeconds != plain.PhantomSeconds)
                parts.Add(Texts.Format("branch.rule.phantom", Number(strike.PhantomSeconds)));
        }

        private static void FlurryRules(List<string> parts, FlurryEffect flurry, FlurryEffect plain)
        {
            if (flurry.Hits != plain.Hits || flurry.Seconds != plain.Seconds || flurry.Multiplier != plain.Multiplier)
                parts.Add(Texts.Format("branch.rule.flurry", flurry.Hits, Number(flurry.Multiplier), Number(flurry.Seconds)));
            if (flurry.QueueTargets != plain.QueueTargets)
                parts.Add(Texts.Format("branch.rule.queue", flurry.QueueTargets));
            if (flurry.Touches != plain.Touches)
                parts.Add(Texts.Format("branch.rule.touches", flurry.Touches));
        }

        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Percent(double share) => (share * 100).ToString("0.#", CultureInfo.InvariantCulture);

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
