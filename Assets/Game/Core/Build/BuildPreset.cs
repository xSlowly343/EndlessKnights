using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public sealed class BuildPreset
    {
        public BuildPreset(ClassTreeAllocation classTree)
        {
            ClassTree = classTree;
        }

        public ClassTreeAllocation ClassTree { get; set; }
        public List<ClassTreeAllocation?> CommonTrees { get; set; } = new List<ClassTreeAllocation?>();
        public List<GemDef> Skills { get; } = new List<GemDef>();
        public List<GemDef?[]> Supports { get; } = new List<GemDef?[]>();
        public List<SkillSlotRole> Roles { get; } = new List<SkillSlotRole>();

        public GemDef?[] SupportsAt(int index) => index < Supports.Count ? Supports[index] : System.Array.Empty<GemDef?>();

        public SkillSlotRole? RoleAt(int index) => index < Roles.Count ? Roles[index] : SkillLoadout.RoleAtPosition(index);
    }
}
