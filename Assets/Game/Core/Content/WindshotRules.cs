namespace Game.Core.Content
{
    public sealed class WindshotRules
    {
        public static readonly WindshotRules None = new WindshotRules();

        public int ShotEvery { get; private set; } = 6;
        public int ShotEveryMin { get; private set; } = 2;
        public double ShotPer { get; private set; } = 25;
        public double ShotSpend { get; private set; } = 25;
        public double ShotPartyCrit { get; private set; }
        public double ShotPauseSeconds { get; private set; } = 2;
        public double ShotStormMultiplier { get; private set; } = 2;
        public double ShotAutoSeconds { get; private set; } = 3;
        public double ShotAutoMultiplier { get; private set; } = 1;
        public double ShotPierceShare { get; private set; } = 1;
        public double CalmStillSeconds { get; private set; } = 4;
        public double CalmStillMinSeconds { get; private set; } = 1;
        public double CalmCritPenalty { get; private set; } = 0.3;
        public double CalmEternalPenalty { get; private set; } = 0.5;
        public double CalmPartyCritMultiplier { get; private set; }
        public double CalmTensionPerCrit { get; private set; }
        public double CalmDeadOutside { get; private set; } = 1;
        public double CalmDeadShare { get; private set; } = 1;
        public double CalmFirstMultiplier { get; private set; } = 1;
        public int CalmStormHits { get; private set; }
        public double CalmStormMultiplier { get; private set; } = 1;
        public double GustPx { get; private set; } = 3000;
        public double GustPxMin { get; private set; } = 500;
        public int GustHits { get; private set; } = 10;
        public int GustHitsMin { get; private set; } = 1;
        public int GustMax { get; private set; } = 5;
        public double GustTension { get; private set; }
        public double GustPartyCooldown { get; private set; }
        public double GustPartySeconds { get; private set; } = 5;
        public double GustStormSeconds { get; private set; } = 8;
        public int GustStormGusts { get; private set; } = 5;
        public double GustQueueShare { get; private set; } = 1;
        public double GustTimerSeconds { get; private set; } = 6;

        public static WindshotRules Read(ContentNode node)
        {
            var shot = node.Field("shot");
            var calm = node.Field("calm");
            var gust = node.Field("gust");
            return new WindshotRules
            {
                ShotEvery = shot.Field("every").AsInt(1, 1000),
                ShotEveryMin = shot.Field("everyMin").AsInt(1, 1000),
                ShotPer = shot.Field("per").AsNumber(1, 1000),
                ShotSpend = shot.Field("spend").AsNumber(0, 1000),
                ShotPartyCrit = shot.Field("partyCrit").AsNumber(0, 1),
                ShotPauseSeconds = shot.Field("pauseSeconds").AsNumber(0.1, 600),
                ShotStormMultiplier = shot.Field("stormMultiplier").AsNumber(1, 100),
                ShotAutoSeconds = shot.Field("autoSeconds").AsNumber(0.1, 600),
                ShotAutoMultiplier = shot.Field("autoMultiplier").AsNumber(0, 100),
                ShotPierceShare = shot.Field("pierceShare").AsNumber(0, 10),
                CalmStillSeconds = calm.Field("stillSeconds").AsNumber(0.1, 600),
                CalmStillMinSeconds = calm.Field("stillMinSeconds").AsNumber(0.1, 600),
                CalmCritPenalty = calm.Field("critPenalty").AsNumber(0, 1),
                CalmEternalPenalty = calm.Field("eternalPenalty").AsNumber(0, 1),
                CalmPartyCritMultiplier = calm.Field("partyCritMultiplier").AsNumber(0, 10),
                CalmTensionPerCrit = calm.Field("tensionPerCrit").AsNumber(0, 100),
                CalmDeadOutside = calm.Field("deadOutside").AsNumber(0, 1),
                CalmDeadShare = calm.Field("deadShare").AsNumber(0, 1),
                CalmFirstMultiplier = calm.Field("firstMultiplier").AsNumber(1, 100),
                CalmStormHits = calm.Field("stormHits").AsInt(0, 1000),
                CalmStormMultiplier = calm.Field("stormMultiplier").AsNumber(1, 100),
                GustPx = gust.Field("px").AsNumber(1, 1e7),
                GustPxMin = gust.Field("pxMin").AsNumber(1, 1e7),
                GustHits = gust.Field("hits").AsInt(1, 1000),
                GustHitsMin = gust.Field("hitsMin").AsInt(1, 1000),
                GustMax = gust.Field("max").AsInt(1, 100),
                GustTension = gust.Field("tension").AsNumber(0, 1000),
                GustPartyCooldown = gust.Field("partyCooldown").AsNumber(0, 0.9),
                GustPartySeconds = gust.Field("partySeconds").AsNumber(0, 600),
                GustStormSeconds = gust.Field("stormSeconds").AsNumber(0.1, 600),
                GustStormGusts = gust.Field("stormGusts").AsInt(1, 100),
                GustQueueShare = gust.Field("queueShare").AsNumber(0, 10),
                GustTimerSeconds = gust.Field("timerSeconds").AsNumber(0.1, 600)
            };
        }
    }
}
