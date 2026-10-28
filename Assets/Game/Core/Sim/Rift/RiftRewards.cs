using Game.Core.Content.Rift;

namespace Game.Core.Sim.Rift
{
    public static class RiftRewards
    {
        public static void Apply(GameState state, in RiftReward reward)
        {
            var drops = state.Drops;
            drops.Shards += System.Math.Max(0, reward.Shards);
            drops.UncutGems += System.Math.Max(0, reward.UncutGems);
            drops.Ingots += System.Math.Max(0, reward.Ingots);
        }
    }
}
