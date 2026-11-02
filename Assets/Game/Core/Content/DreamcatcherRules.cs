namespace Game.Core.Content
{
    public sealed class DreamcatcherRules
    {
        public static readonly DreamcatcherRules None = new DreamcatcherRules();

        public double DozeMultiplier { get; private set; } = 1.5;
        public double LucidPower { get; private set; } = 0.3;
        public double NetPx { get; private set; } = 2000;
        public double NetPxMin { get; private set; } = 300;
        public int NetMax { get; private set; } = 3;
        public double NetSeconds { get; private set; } = 3;
        public double NetSecondsMin { get; private set; } = 0.5;
        public double NetTrophy { get; private set; } = 0.2;
        public double AlbumMax { get; private set; } = 0.5;
        public int PageMin { get; private set; } = 10;
        public int PageMinFloor { get; private set; } = 1;
        public double ProphecyPower { get; private set; } = 0.25;
        public double ProphecySeconds { get; private set; } = 10;
        public double AllShare { get; private set; } = 0.5;
        public int BaitHits { get; private set; } = 100;
        public int BaitHitsMin { get; private set; } = 10;
        public int BaitPerCast { get; private set; } = 5;
        public double SpiritHp { get; private set; } = 3;
        public double SpiritHpMax { get; private set; } = 20;
        public int SpiritGems { get; private set; } = 1;
        public long SpiritShards { get; private set; } = 2;
        public double SpiritChest { get; private set; } = 1;
        public double TwinChance { get; private set; } = 0.25;
        public double HornPower { get; private set; } = 0.3;
        public double HornSeconds { get; private set; } = 15;
        public double SpiritGift { get; private set; } = 30;

        public static DreamcatcherRules Read(ContentNode node)
        {
            var common = node.Field("common");
            var net = node.Field("net");
            var album = node.Field("album");
            var bait = node.Field("bait");
            return new DreamcatcherRules
            {
                DozeMultiplier = common.Field("dozeMultiplier").AsNumber(1, 100),
                LucidPower = common.Field("lucidPower").AsNumber(0, 10),
                NetPx = net.Field("px").AsNumber(1, 1e7),
                NetPxMin = net.Field("pxMin").AsNumber(1, 1e7),
                NetMax = net.Field("max").AsInt(1, 100),
                NetSeconds = net.Field("seconds").AsNumber(0.1, 600),
                NetSecondsMin = net.Field("secondsMin").AsNumber(0.1, 600),
                NetTrophy = net.Field("trophy").AsNumber(0, 1),
                AlbumMax = album.Field("max").AsNumber(0, 10),
                PageMin = album.Field("pageMin").AsInt(1, 10_000),
                PageMinFloor = album.Field("pageMinFloor").AsInt(1, 10_000),
                ProphecyPower = album.Field("prophecyPower").AsNumber(0, 10),
                ProphecySeconds = album.Field("prophecySeconds").AsNumber(0, 600),
                AllShare = album.Field("allShare").AsNumber(0, 1),
                BaitHits = bait.Field("hits").AsInt(1, 100_000),
                BaitHitsMin = bait.Field("hitsMin").AsInt(1, 100_000),
                BaitPerCast = bait.Field("perCast").AsInt(0, 10_000),
                SpiritHp = bait.Field("spiritHp").AsNumber(1, 100),
                SpiritHpMax = bait.Field("spiritHpMax").AsNumber(1, 100),
                SpiritGems = bait.Field("gems").AsInt(0, 100),
                SpiritShards = bait.Field("shards").AsInt(0, 100_000),
                SpiritChest = bait.Field("chest").AsNumber(0, 100),
                TwinChance = bait.Field("twinChance").AsNumber(0, 1),
                HornPower = bait.Field("hornPower").AsNumber(0, 10),
                HornSeconds = bait.Field("hornSeconds").AsNumber(0, 600),
                SpiritGift = bait.Field("gift").AsNumber(0, 1000)
            };
        }
    }
}
