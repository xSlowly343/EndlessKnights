namespace Game.Core.Content.Items
{
    public sealed class ShardRules
    {
        private ShardRules(int boss, int specialBoss, int guardian, int dailyLimit, double uncutShare, int uncutCost)
        {
            Boss = boss;
            SpecialBoss = specialBoss;
            Guardian = guardian;
            DailyLimit = dailyLimit;
            UncutShare = uncutShare;
            UncutCost = uncutCost;
        }

        public int Boss { get; }
        public int SpecialBoss { get; }
        public int Guardian { get; }
        public int DailyLimit { get; }
        public double UncutShare { get; }
        public int UncutCost { get; }

        public static readonly ShardRules None = new ShardRules(0, 0, 0, 0, 0, 0);

        public int ForBoss(EnemyType type, bool special) =>
            type == EnemyType.Guardian ? Guardian : special ? SpecialBoss : Boss;

        public static ShardRules Read(ContentNode shards, ContentNode uncut) =>
            new ShardRules(
                shards.Field("boss").AsInt(0, 1000000),
                shards.Field("specialBoss").AsInt(0, 1000000),
                shards.Field("guardian").AsInt(0, 1000000),
                shards.Field("dailyLimit").AsInt(0, 1000000),
                uncut.Field("share").AsNumber(0, 1),
                uncut.Field("shardCost").AsInt(1, 1000000));
    }
}
