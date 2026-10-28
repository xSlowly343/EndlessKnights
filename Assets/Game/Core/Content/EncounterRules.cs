namespace Game.Core.Content
{
    public sealed class EncounterRules
    {
        private EncounterRules(double wandererChance, long wandererMinStage, int jumpMin, int jumpMax, double goldenChance, long goldenMinStage, double goldenSeconds,
            double goldenHpMultiplier, double goldenGold)
        {
            WandererChance = wandererChance;
            WandererMinStage = wandererMinStage;
            JumpMin = jumpMin;
            JumpMax = jumpMax;
            GoldenChance = goldenChance;
            GoldenMinStage = goldenMinStage;
            GoldenSeconds = goldenSeconds;
            GoldenHpMultiplier = goldenHpMultiplier;
            GoldenGold = goldenGold;
        }

        public double WandererChance { get; }
        public long WandererMinStage { get; }
        public int JumpMin { get; }
        public int JumpMax { get; }
        public double GoldenChance { get; }
        public long GoldenMinStage { get; }
        public double GoldenSeconds { get; }
        public double GoldenHpMultiplier { get; }
        public double GoldenGold { get; }

        public static EncounterRules Read(ContentNode node)
        {
            var wanderer = node.Field("wanderer");
            var golden = node.Field("golden");
            var jumpMin = wanderer.Field("jumpMin").AsInt(1, 100000);
            var jumpMax = wanderer.Field("jumpMax").AsInt(1, 100000);
            if (jumpMax < jumpMin)
                throw wanderer.Field("jumpMax").Error($"must not be below jumpMin ({jumpMin})");
            return new EncounterRules(
                wanderer.Field("chance").AsNumber(0, 1),
                wanderer.Field("minStage").AsLong(1),
                jumpMin,
                jumpMax,
                golden.Field("chance").AsNumber(0, 1),
                golden.Field("minStage").AsLong(1),
                golden.Field("seconds").AsNumber(0.1, 600),
                golden.Field("hpMultiplier").AsNumber(1, 1e12),
                golden.Field("goldPerHp").AsNumber(0, 1e6));
        }
    }
}
