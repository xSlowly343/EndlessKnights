namespace Game.Core.Sim.Rift
{
    public sealed class StageProgress
    {
        public long Stage { get; set; } = 1;
        public long HighestStage { get; set; } = 1;
        public int KillsOnStage { get; set; }
        public bool Farming { get; set; }
        public int FarmKills { get; set; }
        public int BossFailsInARow { get; set; }
        public double BossBestFraction { get; set; }

        public static StageProgress Capture(GameState state) => new StageProgress
        {
            Stage = state.Stage,
            HighestStage = state.HighestStage,
            KillsOnStage = state.KillsOnStage,
            Farming = state.Farming,
            FarmKills = state.FarmKills,
            BossFailsInARow = state.BossFailsInARow,
            BossBestFraction = state.BossBestFraction
        };

        public void Restore(GameState state)
        {
            state.Stage = Stage;
            state.HighestStage = HighestStage;
            state.KillsOnStage = KillsOnStage;
            state.Farming = Farming;
            state.FarmKills = FarmKills;
            state.BossFailsInARow = BossFailsInARow;
            state.BossBestFraction = BossBestFraction;
            state.BossTicksLeft = 0;
            state.ClearQueue();
        }
    }
}
