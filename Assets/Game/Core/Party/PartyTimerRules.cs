using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyTimerRules
    {
        private PartyTimerRules(double minBossRate, double repeatMaxSeconds, double repeatCooldownSeconds)
        {
            MinBossRate = minBossRate;
            RepeatMaxSeconds = repeatMaxSeconds;
            RepeatCooldownSeconds = repeatCooldownSeconds;
        }

        public double MinBossRate { get; }
        public double RepeatMaxSeconds { get; }
        public double RepeatCooldownSeconds { get; }

        public static PartyTimerRules Read(ContentNode node) => new PartyTimerRules(
            node.Field("minBossRate").AsNumber(0, 1),
            node.Field("repeatMaxSeconds").AsNumber(0.1, PartyEncounter.MaxRepeatSeconds),
            node.Field("repeatCooldownSeconds").AsNumber(1, 600));
    }
}
