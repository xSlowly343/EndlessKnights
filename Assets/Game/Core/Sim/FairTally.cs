using Game.Core.Content;

namespace Game.Core.Sim
{
    public readonly struct FairGarden
    {
        public FairGarden(long harvests, long mutations)
        {
            Harvests = harvests;
            Mutations = mutations;
        }

        public long Harvests { get; }
        public long Mutations { get; }
        public bool Any => Harvests > 0 || Mutations > 0;
    }

    public static class FairTally
    {
        public static FairGarden Garden(GameState state) =>
            new FairGarden(System.Math.Max(0, state.Garden.Harvests - state.Fair.SentHarvests), System.Math.Max(0, state.Garden.Mutations - state.Fair.SentMutations));

        public static void GardenSent(GameState state, FairGarden sent)
        {
            state.Fair.SentHarvests += sent.Harvests;
            state.Fair.SentMutations += sent.Mutations;
        }

        public static double FishScore(FishingRules rules, string fish, double cm)
        {
            var max = rules.Fish(fish)?.MaxCm ?? rules.Legend(fish)?.Cm ?? 0;
            return max <= 0 ? 0 : System.Math.Round(cm / max * 1000) / 10;
        }
    }
}
