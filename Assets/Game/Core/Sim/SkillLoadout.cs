using System.Collections.Generic;
using Game.Core.Content.Gems;

namespace Game.Core.Sim
{
    public static class SkillLoadout
    {
        public const int HandSlots = 2;
        public const int AutoSlots = 3;
        public const int AuraSlots = 4;
        public const int MaxSlots = HandSlots + AutoSlots + AuraSlots;
        public const int NoSlot = -1;

        private static readonly SkillSlotRole[] AuraOrder = { SkillSlotRole.Aura, SkillSlotRole.Left, SkillSlotRole.Right, SkillSlotRole.Auto };
        private static readonly SkillSlotRole[] SkillOrder = { SkillSlotRole.Left, SkillSlotRole.Right, SkillSlotRole.Auto };

        public static bool IsAura(GemDef skill) => skill.Type == SkillType.Aura;

        public static bool Accepts(SkillSlotRole role, GemDef skill) => role != SkillSlotRole.Aura || IsAura(skill);

        public static bool CastsByItself(SkillSlotRole role) => role == SkillSlotRole.Auto || role == SkillSlotRole.Aura;

        public static int Capacity(SkillSlotRole role, int autoSlots, int auraSlots) => role switch
        {
            SkillSlotRole.Auto => autoSlots,
            SkillSlotRole.Aura => auraSlots,
            _ => 1
        };

        public static int IndexOf(IReadOnlyList<SkillSlot> skills, SkillSlotRole role)
        {
            for (var i = 0; i < skills.Count; i++)
            {
                if (skills[i].Role == role)
                    return i;
            }
            return NoSlot;
        }

        public static int Count(IReadOnlyList<SkillSlot> skills, SkillSlotRole role)
        {
            var count = 0;
            for (var i = 0; i < skills.Count; i++)
            {
                if (skills[i].Role == role)
                    count++;
            }
            return count;
        }

        public static int RowOf(IReadOnlyList<SkillSlot> skills, GemDef skill)
        {
            for (var i = 0; i < skills.Count; i++)
            {
                if (ReferenceEquals(skills[i].Skill, skill))
                    return i;
            }
            return NoSlot;
        }

        public static bool HasRoom(IReadOnlyList<SkillSlot> skills, SkillSlotRole role, int autoSlots, int auraSlots) =>
            Count(skills, role) < Capacity(role, autoSlots, auraSlots);

        public static SkillSlotRole? FreeRole(IReadOnlyList<SkillSlot> skills, GemDef skill, int autoSlots = AutoSlots, int auraSlots = AuraSlots)
        {
            foreach (var role in IsAura(skill) ? AuraOrder : SkillOrder)
            {
                if (HasRoom(skills, role, autoSlots, auraSlots))
                    return role;
            }
            return null;
        }

        public static SkillSlotRole? RoleAtPosition(int position) => position switch
        {
            0 => SkillSlotRole.Left,
            1 => SkillSlotRole.Right,
            _ => null
        };
    }
}
