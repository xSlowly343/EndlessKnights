using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public static class SkillShowKind
    {
        public static bool CastByHand(IReadOnlyList<SkillSlot> skills, GemDef gem)
        {
            for (var i = 0; i < skills.Count; i++)
            {
                if (ReferenceEquals(skills[i].Skill, gem))
                    return skills[i].Role == SkillSlotRole.Left || skills[i].Role == SkillSlotRole.Right;
            }
            return false;
        }
    }
}
