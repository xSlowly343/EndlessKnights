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
        public double StageBarsShare { get; private set; }

        public double BarsPerSecondAt(int enemiesOnStage) => System.Math.Max(BarsPerSecond, enemiesOnStage * StageBarsShare);

        public double BurstAt(int enemiesOnStage) => BurstBars * BarsPerSecondAt(enemiesOnStage) / BarsPerSecond;

        public static PartyCapRules Read(ContentNode node) => new PartyCapRules(
            node.Field("barsPerSecond").AsNumber(0.1, 100),
            node.Field("burstBars").AsNumber(1, 100),
            node.Field("hitBurstSeconds").AsNumber(0.5, 60),
            node.Field("flagsBeforeReport").AsInt(1, 100000))
        {
            StageBarsShare = node.HasField("stageBarsShare") ? node.Field("stageBarsShare").AsNumber(0, 10) : 0
        };
    }
}
