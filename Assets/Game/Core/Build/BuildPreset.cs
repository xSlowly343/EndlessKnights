using System.Collections.Generic;
using Game.Core.Content.Gems;

namespace Game.Core.Build
{
    public sealed class BuildPreset
    {
        public BuildPreset(TreeAllocation tree)
        {
            Tree = tree;
        }

        public TreeAllocation Tree { get; set; }
        public List<(GemDef Skill, GemDef? Trigger)> Skills { get; } = new List<(GemDef, GemDef?)>();
        public List<GemDef?[]> Supports { get; } = new List<GemDef?[]>();

        public GemDef?[] SupportsAt(int index) => index < Supports.Count ? Supports[index] : System.Array.Empty<GemDef?>();
    }
}
