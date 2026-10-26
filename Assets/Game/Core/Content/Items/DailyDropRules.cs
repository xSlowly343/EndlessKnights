namespace Game.Core.Content.Items
{
    public sealed class DailyDropRules
    {
        private DailyDropRules(double chance, int limit, int maxLimit, double recordShare, double offlineChance)
        {
            Chance = chance;
            Limit = limit;
            MaxLimit = maxLimit;
            RecordShare = recordShare;
            OfflineChance = offlineChance;
        }

        public double Chance { get; }
        public int Limit { get; }
        public int MaxLimit { get; }
        public double RecordShare { get; }
        public double OfflineChance { get; }

        public bool Allows(long stage, long recordStage) => stage >= RecordShare * recordStage;

        public static DailyDropRules Read(ContentNode node)
        {
            var limit = node.Field("limit").AsInt(0, 10000);
            var maxLimit = node.Field("maxLimit").AsInt(0, 10000);
            if (maxLimit < limit)
                throw node.Field("maxLimit").Error($"must not be below limit ({limit})");
            return new DailyDropRules(
                node.Field("chance").AsNumber(0, 1),
                limit,
                maxLimit,
                node.Field("recordShare").AsNumber(0, 1),
                node.Field("offlineChance").AsNumber(0, 1));
        }
    }
}
