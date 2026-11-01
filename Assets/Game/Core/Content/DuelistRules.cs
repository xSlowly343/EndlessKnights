namespace Game.Core.Content
{
    public sealed class DuelistRules
    {
        public static readonly DuelistRules None = new DuelistRules();

        public double BreachEverySeconds { get; private set; } = 4;
        public double BreachEveryMinSeconds { get; private set; } = 1;
        public double BreachSeconds { get; private set; } = 1;
        public double BreachHonor { get; private set; }
        public double BreachPartyDamage { get; private set; }
        public double BreachPressMax { get; private set; } = 2;
        public double BreachExposeSeconds { get; private set; } = 4;
        public double BreachExposeMax { get; private set; }
        public double BreachBossSooner { get; private set; } = 1;
        public double BreachOutsidePenalty { get; private set; } = 0.5;
        public double BreachDoubleShare { get; private set; } = 1;
        public int ToucheEvery { get; private set; } = 10;
        public int ToucheEveryMin { get; private set; } = 2;
        public int ToucheMarks { get; private set; } = 5;
        public int ToucheMarksMin { get; private set; } = 1;
        public double ToucheBossShare { get; private set; } = 0.5;
        public double ToucheExecuteMax { get; private set; } = 0.5;
        public double ToucheMarkDamageMax { get; private set; } = 1;
        public double TouchePartyGold { get; private set; }
        public double TouchePartyGoldSeconds { get; private set; } = 15;
        public double TouchePublicSeconds { get; private set; } = 5;
        public double PhantomHitsPerSecond { get; private set; } = 4;
        public double PhantomSelfPenalty { get; private set; }
        public double PhantomPartyEverySeconds { get; private set; } = 30;
        public double PhantomPartySeconds { get; private set; } = 5;
        public double PhantomPartyDamage { get; private set; }
        public double PhantomPauseAfterSeconds { get; private set; } = 1;
        public double PhantomPauseHitsPerSecond { get; private set; }
        public double PhantomFeintCrit { get; private set; }

        public static DuelistRules Read(ContentNode node)
        {
            var breach = node.Field("breach");
            var touche = node.Field("touche");
            var phantom = node.Field("phantom");
            return new DuelistRules
            {
                BreachEverySeconds = breach.Field("everySeconds").AsNumber(0.1, 600),
                BreachEveryMinSeconds = breach.Field("everyMinSeconds").AsNumber(0.1, 600),
                BreachSeconds = breach.Field("seconds").AsNumber(0.1, 600),
                BreachHonor = breach.Field("honor").AsNumber(0, 1000),
                BreachPartyDamage = breach.Field("partyDamage").AsNumber(0, 10),
                BreachPressMax = breach.Field("pressMax").AsNumber(1, 100),
                BreachExposeSeconds = breach.Field("exposeSeconds").AsNumber(0.1, 600),
                BreachExposeMax = breach.Field("exposeMax").AsNumber(0, 10),
                BreachBossSooner = breach.Field("bossSoonerSeconds").AsNumber(0, 600),
                BreachOutsidePenalty = breach.Field("outsidePenalty").AsNumber(0, 1),
                BreachDoubleShare = breach.Field("doubleShare").AsNumber(0, 1),
                ToucheEvery = touche.Field("every").AsInt(1, 1000),
                ToucheEveryMin = touche.Field("everyMin").AsInt(1, 1000),
                ToucheMarks = touche.Field("marks").AsInt(1, 100),
                ToucheMarksMin = touche.Field("marksMin").AsInt(1, 100),
                ToucheBossShare = touche.Field("bossShare").AsNumber(0, 1),
                ToucheExecuteMax = touche.Field("executeMax").AsNumber(0, 1),
                ToucheMarkDamageMax = touche.Field("markDamageMax").AsNumber(0, 100),
                TouchePartyGold = touche.Field("partyGold").AsNumber(0, 100),
                TouchePartyGoldSeconds = touche.Field("partyGoldSeconds").AsNumber(0, 600),
                TouchePublicSeconds = touche.Field("publicSeconds").AsNumber(0, 600),
                PhantomHitsPerSecond = phantom.Field("hitsPerSecond").AsNumber(0, 100),
                PhantomSelfPenalty = phantom.Field("selfPenalty").AsNumber(0, 1),
                PhantomPartyEverySeconds = phantom.Field("partyEverySeconds").AsNumber(1, 3600),
                PhantomPartySeconds = phantom.Field("partySeconds").AsNumber(0, 3600),
                PhantomPartyDamage = phantom.Field("partyDamage").AsNumber(0, 10),
                PhantomPauseAfterSeconds = phantom.Field("pauseAfterSeconds").AsNumber(0.1, 600),
                PhantomPauseHitsPerSecond = phantom.Field("pauseHitsPerSecond").AsNumber(0, 100),
                PhantomFeintCrit = phantom.Field("feintCrit").AsNumber(0, 1)
            };
        }
    }
}
