using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class DungeonKey
    {
        public DungeonKey(int tier, IReadOnlyList<string> modifiers)
        {
            Tier = tier;
            Modifiers = modifiers;
        }

        public int Tier { get; }
        public IReadOnlyList<string> Modifiers { get; }
    }
}
