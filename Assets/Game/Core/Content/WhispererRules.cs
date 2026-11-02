namespace Game.Core.Content
{
    public sealed class WhispererRules
    {
        public static readonly WhispererRules None = new WhispererRules();

        public int KnotHits { get; private set; } = 25;
        public int KnotHitsMin { get; private set; } = 5;
        public double AllyHitShare { get; private set; } = 1;
        public double KnotSeventh { get; private set; } = 2;
        public double KnotTriple { get; private set; } = 3;
        public double KnotSlow { get; private set; } = 0.05;
        public double KnotCritMultiplier { get; private set; } = 0.1;
        public double KnotGold { get; private set; } = 0.1;
        public double KnotBlast { get; private set; } = 10;
        public double KnotWhisper { get; private set; } = 10;
        public double KnotEffectSeconds { get; private set; } = 2;
        public int NameHits { get; private set; } = 300;
        public int NameHitsMin { get; private set; } = 30;
        public int SyllableHits { get; private set; } = 50;
        public int SyllableHitsMin { get; private set; } = 5;
        public double Syllable { get; private set; } = 0.05;
        public double NameCallSeconds { get; private set; } = 30;
        public double NameBlast { get; private set; } = 20;
        public double NameTimeSeconds { get; private set; } = 5;
        public double NameEffectSeconds { get; private set; } = 2;
        public double VowCurse { get; private set; } = 3;
        public double VowWhisper { get; private set; } = 0.5;
        public int HushHits { get; private set; } = 10;
        public int HushHitsMin { get; private set; } = 3;
        public int HushMax { get; private set; } = 3;
        public double HushExtendSeconds { get; private set; } = 1;
        public double VowGrowPerHit { get; private set; } = 0.01;
        public double VowGrowMax { get; private set; } = 0.5;
        public double VowBreakEverySeconds { get; private set; } = 60;
        public double VowBreakSeconds { get; private set; } = 10;
        public double VowBreakCurse { get; private set; } = 0.5;
        public int VowQueueHits { get; private set; } = 100;
        public double VowQueueMultiplier { get; private set; } = 5;
        public double CurseSeconds { get; private set; } = 8;
        public double CurseEffectSeconds { get; private set; } = 2;

        public static WhispererRules Read(ContentNode node)
        {
            var knots = node.Field("knots");
            var name = node.Field("name");
            var vow = node.Field("vow");
            var curse = node.Field("curse");
            return new WhispererRules
            {
                KnotHits = knots.Field("hits").AsInt(1, 10_000),
                KnotHitsMin = knots.Field("hitsMin").AsInt(1, 10_000),
                AllyHitShare = knots.Field("allyHitShare").AsNumber(0, 10),
                KnotSeventh = knots.Field("seventh").AsNumber(1, 100),
                KnotTriple = knots.Field("triple").AsNumber(1, 100),
                KnotSlow = knots.Field("slow").AsNumber(0, 1),
                KnotCritMultiplier = knots.Field("critMultiplier").AsNumber(0, 10),
                KnotGold = knots.Field("gold").AsNumber(0, 10),
                KnotBlast = knots.Field("blast").AsNumber(0, 1000),
                KnotWhisper = knots.Field("whisper").AsNumber(0, 1000),
                KnotEffectSeconds = knots.Field("effectSeconds").AsNumber(0.1, 60),
                NameHits = name.Field("hits").AsInt(1, 100_000),
                NameHitsMin = name.Field("hitsMin").AsInt(1, 100_000),
                SyllableHits = name.Field("syllableHits").AsInt(1, 255),
                SyllableHitsMin = name.Field("syllableHitsMin").AsInt(1, 255),
                Syllable = name.Field("syllable").AsNumber(0, 10),
                NameCallSeconds = name.Field("callSeconds").AsNumber(1, 3600),
                NameBlast = name.Field("blast").AsNumber(0, 1000),
                NameTimeSeconds = name.Field("timeSeconds").AsNumber(0, 600),
                NameEffectSeconds = name.Field("effectSeconds").AsNumber(0.1, 60),
                VowCurse = vow.Field("curse").AsNumber(1, 100),
                VowWhisper = vow.Field("whisper").AsNumber(0, 100),
                HushHits = vow.Field("hushHits").AsInt(1, 10_000),
                HushHitsMin = vow.Field("hushHitsMin").AsInt(1, 10_000),
                HushMax = vow.Field("hushMax").AsInt(1, 255),
                HushExtendSeconds = vow.Field("extendSeconds").AsNumber(0, 60),
                VowGrowPerHit = vow.Field("growPerHit").AsNumber(0, 1),
                VowGrowMax = vow.Field("growMax").AsNumber(0, 10),
                VowBreakEverySeconds = vow.Field("breakEverySeconds").AsNumber(1, 3600),
                VowBreakSeconds = vow.Field("breakSeconds").AsNumber(0, 600),
                VowBreakCurse = vow.Field("breakCurse").AsNumber(0, 1),
                VowQueueHits = vow.Field("queueHits").AsInt(1, 100_000),
                VowQueueMultiplier = vow.Field("queueMultiplier").AsNumber(0, 1000),
                CurseSeconds = curse.Field("seconds").AsNumber(0.1, 600),
                CurseEffectSeconds = curse.Field("effectSeconds").AsNumber(0.1, 60)
            };
        }
    }
}
