using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyColossusRules
    {
        public static readonly PartyColossusRules None = new PartyColossusRules();

        public double PointsPerBoss { get; private set; } = 100;
        public double Background { get; private set; }
        public double Offline { get; private set; }
        public double HpBase { get; private set; } = 1;
        public double HpGrowth { get; private set; } = 1;
        public int CeilingBase { get; private set; }
        public int CeilingStep { get; private set; }
        public double MaxPointsPerSecond { get; private set; }
        public double MaxOfflinePointsPerSecond { get; private set; }
        public double ReportSeconds { get; private set; } = 300;
        public double ExperiencePerPercent { get; private set; }

        public double Hp(int tier) => HpBase * System.Math.Pow(HpGrowth, System.Math.Max(1, tier) - 1);

        public int Ceiling(PartyGrowthRules growth, int partyLevel)
        {
            if (!growth.Opened(PartyUnlockKind.Colossus, partyLevel))
                return 0;
            return CeilingBase + CeilingStep * growth.CountOpened(PartyUnlockKind.ColossusTier, partyLevel);
        }

        public static PartyColossusRules Read(ContentNode node)
        {
            var hp = node.Field("hp");
            return new PartyColossusRules
            {
                PointsPerBoss = node.Field("pointsPerBoss").AsNumber(1, 1e6),
                Background = node.Field("background").AsNumber(0, 1),
                Offline = node.Field("offline").AsNumber(0, 1),
                HpBase = hp.Field("base").AsNumber(1, 1e15),
                HpGrowth = hp.Field("growth").AsNumber(1, 100),
                CeilingBase = node.Field("ceilingBase").AsInt(1, 100_000),
                CeilingStep = node.Field("ceilingStep").AsInt(0, 100_000),
                MaxPointsPerSecond = node.Field("maxPointsPerSecond").AsNumber(0, 1e9),
                MaxOfflinePointsPerSecond = node.Field("maxOfflinePointsPerSecond").AsNumber(0, 1e9),
                ReportSeconds = node.Field("reportSeconds").AsNumber(10, 86400),
                ExperiencePerPercent = node.Field("experiencePerPercent").AsNumber(0, 1e6)
            };
        }
    }
}
