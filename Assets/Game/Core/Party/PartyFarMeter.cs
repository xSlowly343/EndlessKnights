using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyFarMeter
    {
        public const string Dungeons = "dungeons";
        public const string Rift = "rift";

        public int DungeonBosses { get; private set; }
        public int DungeonDepth { get; private set; }
        public int RiftBosses { get; private set; }
        public int RiftDepth { get; private set; }
        public bool Pending => DungeonBosses > 0 || RiftBosses > 0;

        public void Note(GameState state, long stage)
        {
            var dungeon = state.Dungeons.Active;
            if (dungeon != null)
            {
                if (state.Dungeons.Party)
                    return;
                DungeonBosses++;
                DungeonDepth = System.Math.Max(DungeonDepth, dungeon.Tier);
                return;
            }
            if (!state.Rift.Inside)
                return;
            RiftBosses++;
            RiftDepth = (int)System.Math.Max(RiftDepth, System.Math.Min(int.MaxValue, stage));
        }

        public (int Bosses, int Depth) Take(string source)
        {
            if (source == Dungeons)
            {
                var dungeons = (DungeonBosses, DungeonDepth);
                DungeonBosses = 0;
                DungeonDepth = 0;
                return dungeons;
            }
            var rift = (RiftBosses, RiftDepth);
            RiftBosses = 0;
            RiftDepth = 0;
            return rift;
        }

        public void Return(string source, int bosses, int depth)
        {
            if (bosses <= 0)
                return;
            if (source == Dungeons)
            {
                DungeonBosses += bosses;
                DungeonDepth = System.Math.Max(DungeonDepth, depth);
                return;
            }
            RiftBosses += bosses;
            RiftDepth = System.Math.Max(RiftDepth, depth);
        }
    }
}
