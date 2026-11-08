namespace Game.Core.Content
{
    public sealed class RushRules
    {
        public RushRules(double recordShare, int stagesPerKill)
        {
            RecordShare = recordShare;
            StagesPerKill = stagesPerKill;
        }

        public double RecordShare { get; }
        public int StagesPerKill { get; }

        public long CeilingFor(long recordStage) => (long)System.Math.Floor(recordStage * RecordShare);

        public static RushRules Read(ContentNode node) =>
            new RushRules(node.Field("recordShare").AsNumber(0, 1), node.Field("stagesPerKill").AsInt(1, 1000));
    }
}
