using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public sealed class SimulationResult
    {
        public SimulationResult(IReadOnlyList<StageRecord> stages, long finalStage, long highestStage, BigNumber goldEarned, int bossFails, double seconds, bool stuck)
        {
            Stages = stages;
            FinalStage = finalStage;
            HighestStage = highestStage;
            GoldEarned = goldEarned;
            BossFails = bossFails;
            Seconds = seconds;
            Stuck = stuck;
        }

        public IReadOnlyList<StageRecord> Stages { get; }
        public long FinalStage { get; }
        public long HighestStage { get; }
        public BigNumber GoldEarned { get; }
        public int BossFails { get; }
        public double Seconds { get; }
        public bool Stuck { get; }
    }
}
