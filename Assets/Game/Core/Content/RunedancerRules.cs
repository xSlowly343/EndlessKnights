namespace Game.Core.Content
{
    public sealed class RunedancerRules
    {
        public static readonly RunedancerRules None = new RunedancerRules();

        public int WordLength { get; private set; } = 5;
        public int WordLengthMin { get; private set; } = 2;
        public double FadeSeconds { get; private set; } = 10;
        public double SealSeconds { get; private set; } = 4;
        public double SealDamage { get; private set; }
        public double WeaveSeconds { get; private set; } = 6;
        public double ShadowAfterSeconds { get; private set; } = 1;
        public double ShadowHitsPerSecond { get; private set; }
        public int BladeEvery { get; private set; } = 3;
        public int BladeMax { get; private set; } = 12;
        public double BladePartyCrit { get; private set; }
        public double BladeFallSeconds { get; private set; } = 1;
        public double BladeShardMultiplier { get; private set; } = 2;
        public int WhirlMax { get; private set; } = 10;
        public int WhirlPalmMax { get; private set; } = 5;
        public double WhirlHitsPerSecond { get; private set; } = 1;
        public double WhirlPartyDamage { get; private set; }
        public double WhirlGustSeconds { get; private set; } = 2;
        public double WhirlEyeMultiplier { get; private set; } = 2;

        public static RunedancerRules Read(ContentNode node)
        {
            var runes = node.Field("runes");
            var blades = node.Field("blades");
            var whirl = node.Field("whirl");
            return new RunedancerRules
            {
                WordLength = runes.Field("wordLength").AsInt(2, 50),
                WordLengthMin = runes.Field("wordLengthMin").AsInt(1, 50),
                FadeSeconds = runes.Field("fadeSeconds").AsNumber(0.1, 600),
                SealSeconds = runes.Field("sealSeconds").AsNumber(0, 600),
                SealDamage = runes.Field("sealDamage").AsNumber(0, 10),
                WeaveSeconds = runes.Field("weaveSeconds").AsNumber(0, 600),
                ShadowAfterSeconds = runes.Field("shadowAfterSeconds").AsNumber(0.1, 600),
                ShadowHitsPerSecond = runes.Field("shadowHitsPerSecond").AsNumber(0, 100),
                BladeEvery = blades.Field("every").AsInt(1, 1000),
                BladeMax = blades.Field("max").AsInt(1, 1000),
                BladePartyCrit = blades.Field("partyCritPerBlade").AsNumber(0, 1),
                BladeFallSeconds = blades.Field("fallSeconds").AsNumber(0.1, 600),
                BladeShardMultiplier = blades.Field("shardMultiplier").AsNumber(0, 1000),
                WhirlMax = whirl.Field("max").AsInt(1, 1000),
                WhirlPalmMax = whirl.Field("palmMax").AsInt(1, 1000),
                WhirlHitsPerSecond = whirl.Field("hitsPerSecond").AsNumber(0, 100),
                WhirlPartyDamage = whirl.Field("partyDamagePerBlade").AsNumber(0, 1),
                WhirlGustSeconds = whirl.Field("gustSeconds").AsNumber(0.1, 600),
                WhirlEyeMultiplier = whirl.Field("eyeMultiplier").AsNumber(1, 1000)
            };
        }
    }
}
