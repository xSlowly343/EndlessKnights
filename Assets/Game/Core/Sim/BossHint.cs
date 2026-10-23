using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;

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

        public static BossHint? For(GameState state, BossHintRules rules, PrestigeSystem prestige, StrikeUpgradeSystem upgrades)
        {
            if (state.BossFailsInARow < rules.AfterFails)
                return null;
            var options = AlwaysOpen;
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
                UnknownDamage ? "Урона пока слишком мало, чтобы ранить босса" : $"Тебе не хватает ~{MissingPercent}% урона",
                "Варианты:"
            };
            if (Has(BossHintOptions.Prestige))
                lines.Add("• Престиж: +" + NumberFormatter.Format(EchoGain, format) + " Эха");
            if (Has(BossHintOptions.StrikeUpgrade))
                lines.Add("• Улучшить удар за золото");
            lines.Add("• Сменить билд или пресет");
            lines.Add("• Позвать друга");
            lines.Add("• Пофармить этап и вернуться");
            return lines;
        }
    }
}
