namespace Game.Core.Content
{
    public sealed class PierceRules
    {
        public double MaxShare { get; private set; } = 1;
        public int MaxEnemySkip { get; private set; } = 4;
        public double MaxSplashShare { get; private set; } = 1;
        public int MaxSplashTargets { get; private set; } = 3;
        public double MaxBossPierce { get; private set; } = 0.5;
        public double BossPierceHpShare { get; private set; } = 0.5;
        public double MaxStageCarry { get; private set; } = 1;
        public double MaxStageSkipChance { get; private set; } = 0.15;
        public int StageSkipStages { get; private set; } = 10;

        public static PierceRules Read(ContentNode node) => new PierceRules
        {
            MaxShare = Number(node, "maxShare", 1, 0, 1),
            MaxEnemySkip = Whole(node, "maxEnemySkip", 4, 0, 1000),
            MaxSplashShare = Number(node, "maxSplashShare", 1, 0, 10),
            MaxSplashTargets = Whole(node, "maxSplashTargets", 3, 0, Sim.GameState.QueueDepth),
            MaxBossPierce = Number(node, "maxBossPierce", 0.5, 0, 1),
            BossPierceHpShare = Number(node, "bossPierceHpShare", 0.5, 0, 0.99),
            MaxStageCarry = Number(node, "maxStageCarry", 1, 0, 1),
            MaxStageSkipChance = Number(node, "maxStageSkipChance", 0.15, 0, 1),
            StageSkipStages = Whole(node, "stageSkipStages", 10, 0, 100000)
        };

        private static double Number(ContentNode node, string name, double fallback, double min, double max) =>
            node.HasField(name) ? node.Field(name).AsNumber(min, max) : fallback;

        private static int Whole(ContentNode node, string name, int fallback, int min, int max) =>
            node.HasField(name) ? node.Field(name).AsInt(min, max) : fallback;
    }
}
