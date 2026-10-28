using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyCapRules
    {
        private PartyCapRules(double barsPerSecond, double burstBars, double hitBurstSeconds, int flagsBeforeReport)
        {
            HitBurstSeconds = hitBurstSeconds;
            BarsPerSecond = barsPerSecond;
            BurstBars = burstBars;
            FlagsBeforeReport = flagsBeforeReport;
        }

        public double BarsPerSecond { get; }
        public double BurstBars { get; }
        public double HitBurstSeconds { get; }
        public int FlagsBeforeReport { get; }

        public static PartyCapRules Read(ContentNode node) => new PartyCapRules(
            node.Field("barsPerSecond").AsNumber(0.1, 100),
            node.Field("burstBars").AsNumber(1, 100),
            node.Field("hitBurstSeconds").AsNumber(0.5, 60),
            node.Field("flagsBeforeReport").AsInt(1, 100000));
    }
}
