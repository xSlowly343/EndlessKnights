namespace Game.Core.Content
{
    public sealed class DowserRules
    {
        public static readonly DowserRules None = new DowserRules();

        public int VeinRange { get; private set; } = 50;
        public int VeinNearRange { get; private set; } = 25;
        public double VeinStrike { get; private set; } = 10;
        public double VeinCritSeconds { get; private set; } = 10;
        public double VeinGemChance { get; private set; } = 0.25;
        public double VeinHintStep { get; private set; } = 0.2;
        public double VeinHintSeconds { get; private set; } = 5;
        public double LuckMax { get; private set; } = 1;
        public double LuckBoss { get; private set; } = 3;
        public int LuckUniqueEvery { get; private set; } = 5;
        public double LuckGoldPerKill { get; private set; } = 0.01;
        public double LuckGoldMax { get; private set; } = 0.3;
        public double LuckHourSeconds { get; private set; } = 3600;
        public long LuckShards { get; private set; } = 5;
        public double LuckRelaySeconds { get; private set; } = 30;
        public double LuckRelayStep { get; private set; } = 0.02;
        public double ArtelPenalty { get; private set; } = 0.4;
        public int ArtelNuggets { get; private set; } = 10;
        public int ArtelNuggetsMin { get; private set; } = 3;
        public double ArtelItemChance { get; private set; } = 0.1;
        public double ArtelItemRich { get; private set; } = 0.25;
        public long ArtelShards { get; private set; } = 10;
        public double ArtelCallSeconds { get; private set; } = 30;

        public static DowserRules Read(ContentNode node)
        {
            var vein = node.Field("vein");
            var luck = node.Field("luck");
            var artel = node.Field("artel");
            return new DowserRules
            {
                VeinRange = vein.Field("range").AsInt(1, 10_000),
                VeinNearRange = vein.Field("nearRange").AsInt(1, 10_000),
                VeinStrike = vein.Field("strike").AsNumber(0, 1000),
                VeinCritSeconds = vein.Field("critSeconds").AsNumber(0, 600),
                VeinGemChance = vein.Field("gemChance").AsNumber(0, 1),
                VeinHintStep = vein.Field("hintStep").AsNumber(0.01, 1),
                VeinHintSeconds = vein.Field("hintSeconds").AsNumber(0.1, 600),
                LuckMax = luck.Field("max").AsNumber(0, 1),
                LuckBoss = luck.Field("boss").AsNumber(1, 100),
                LuckUniqueEvery = luck.Field("uniqueEvery").AsInt(1, 1000),
                LuckGoldPerKill = luck.Field("goldPerKill").AsNumber(0, 1),
                LuckGoldMax = luck.Field("goldMax").AsNumber(0, 10),
                LuckHourSeconds = luck.Field("hourSeconds").AsNumber(60, 86_400),
                LuckShards = luck.Field("shards").AsInt(0, 10_000),
                LuckRelaySeconds = luck.Field("relaySeconds").AsNumber(1, 3600),
                LuckRelayStep = luck.Field("relayStep").AsNumber(0.001, 1),
                ArtelPenalty = artel.Field("penalty").AsNumber(0, 1),
                ArtelNuggets = artel.Field("nuggets").AsInt(1, 1000),
                ArtelNuggetsMin = artel.Field("nuggetsMin").AsInt(1, 1000),
                ArtelItemChance = artel.Field("itemChance").AsNumber(0, 1),
                ArtelItemRich = artel.Field("itemRich").AsNumber(0, 1),
                ArtelShards = artel.Field("shards").AsInt(0, 10_000),
                ArtelCallSeconds = artel.Field("callSeconds").AsNumber(1, 3600)
            };
        }
    }
}
