using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyTimerRules
    {
        private PartyTimerRules(double minBossRate, double repeatMaxSeconds, double repeatCooldownSeconds, double sharedRate, int sharedFighters)
        {
            MinBossRate = minBossRate;
            SharedRate = sharedRate;
            SharedFighters = sharedFighters;
            RepeatMaxSeconds = repeatMaxSeconds;
            RepeatCooldownSeconds = repeatCooldownSeconds;
        }

        public double MinBossRate { get; }
        public double RepeatMaxSeconds { get; }
        public double RepeatCooldownSeconds { get; }
        public double SharedRate { get; }
        public int SharedFighters { get; }

        public static PartyTimerRules Read(ContentNode node) => new PartyTimerRules(
            node.Field("minBossRate").AsNumber(0, 1),
            node.Field("repeatMaxSeconds").AsNumber(0.1, PartyEncounter.MaxRepeatSeconds),
            node.Field("repeatCooldownSeconds").AsNumber(1, 600),
            node.Field("sharedRate").AsNumber(0, 1),
            node.Field("sharedFighters").AsInt(2, PartyRules.MaxSlots));
    }
}
