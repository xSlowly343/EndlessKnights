using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Text;

namespace Game.Core.Sim
{
    public sealed class BossHint
    {
        private const BossHintOptions AlwaysOpen = BossHintOptions.ChangeBuild | BossHintOptions.CallFriend | BossHintOptions.FarmStage;

        private BossHint(long stage, int missingPercent, bool unknownDamage, BossHintOptions options, BigNumber echoGain)
        {
            Stage = stage;
            MissingPercent = missingPercent;
            UnknownDamage = unknownDamage;
            Options = options;
            EchoGain = echoGain;
        }

        public long Stage { get; }
        public int MissingPercent { get; }
        public bool UnknownDamage { get; }
        public BossHintOptions Options { get; }
        public BigNumber EchoGain { get; }

        public bool Has(BossHintOptions option) => (Options & option) == option;

        public static BossHint? For(GameState state, BossHintRules rules, PrestigeSystem prestige, StrikeUpgradeSystem upgrades, BossHintOptions unused = BossHintOptions.None)
        {
            if (state.BossFailsInARow < rules.AfterFails)
                return null;
            var options = AlwaysOpen | unused;
            var echo = prestige.Available ? prestige.EchoGain : BigNumber.Zero;
            if (echo.Sign > 0)
                options |= BossHintOptions.Prestige;
            if (upgrades.Affordable() > 0)
                options |= BossHintOptions.StrikeUpgrade;
            var unknown = state.BossBestFraction <= 0;
            return new BossHint(state.Stage, rules.MissingPercent(state.BossBestFraction), unknown, options, echo);
        }

        public IReadOnlyList<string> Lines(NumberFormatSettings format)
        {
            var lines = new List<string>
            {
                UnknownDamage ? Texts.Get("bossHint.unknown") : Texts.Format("bossHint.missing", MissingPercent),
                Texts.Get("bossHint.options")
            };
            if (Has(BossHintOptions.Prestige))
                lines.Add(Texts.Format("bossHint.prestige", NumberFormatter.Format(EchoGain, format)));
            if (Has(BossHintOptions.StrikeUpgrade))
                lines.Add(Texts.Get("bossHint.upgrade"));
            if (Has(BossHintOptions.TreePoints))
                lines.Add(Texts.Get("bossHint.treePoints"));
            if (Has(BossHintOptions.PathChoice))
                lines.Add(Texts.Get("bossHint.path"));
            if (Has(BossHintOptions.EmptyGearSlot))
                lines.Add(Texts.Get("bossHint.gear"));
            if (Has(BossHintOptions.EmptySocket))
                lines.Add(Texts.Get("bossHint.socket"));
            lines.Add(Texts.Get("bossHint.build"));
            lines.Add(Texts.Get("bossHint.friend"));
            lines.Add(Texts.Get("bossHint.farm"));
            return lines;
        }
    }
}
