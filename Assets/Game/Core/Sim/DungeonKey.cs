using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class DungeonKey
    {
        public DungeonKey(int tier, IReadOnlyList<string> modifiers, long stage = 0, int attempts = 1, long rift = 0)
        {
            Tier = tier;
            Modifiers = modifiers;
            Stage = stage;
            Attempts = attempts;
            Rift = rift;
        }

        public int Tier { get; }
        public long Stage { get; }
        public long Rift { get; }
        public bool ForRift => Rift != 0;
        public IReadOnlyList<string> Modifiers { get; }
        public int Attempts { get; set; }
    }
}
