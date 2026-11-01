using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Party.Wire;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public static class SkillBarLayout
    {
        public const int Places = SkillLoadout.MaxSlots;

        public static void Fill(IReadOnlyList<SkillSlot> skills, SkillBook book, SkillBarPlace[] into)
        {
            Place(into, skills, SkillSlotRole.Left, 0, true, 0, 0);
            Place(into, skills, SkillSlotRole.Right, 0, true, 0, 0);
            for (var auto = 0; auto < SkillLoadout.AutoSlots; auto++)
                Place(into, skills, SkillSlotRole.Auto, auto, auto < book.AutoSlotCount, book.UnlockStageOf(SkillLoadout.HandSlots + auto), 0);
            for (var aura = 0; aura < SkillLoadout.AuraSlots; aura++)
                Place(into, skills, SkillSlotRole.Aura, aura, aura < book.AuraSlotCount, 0, book.AuraSlotLevel(aura));
        }

        public static int RowOf(IReadOnlyList<SkillSlot> skills, SkillSlotRole role, int indexInRole)
        {
            var seen = 0;
            for (var row = 0; row < skills.Count; row++)
            {
                if (skills[row].Role != role)
                    continue;
                if (seen == indexInRole)
                    return row;
                seen++;
            }
            return SkillLoadout.NoSlot;
        }

        private static void Place(SkillBarPlace[] into, IReadOnlyList<SkillSlot> skills, SkillSlotRole role, int indexInRole, bool open, long unlockStage, int unlockLevel)
        {
            var position = PartyLoadoutMessage.PositionOf(role, indexInRole);
            into[position] = new SkillBarPlace
            {
                Position = position,
                Role = role,
                Row = open ? RowOf(skills, role, indexInRole) : SkillLoadout.NoSlot,
                Open = open,
                UnlockStage = open ? 0 : unlockStage,
                UnlockLevel = open ? 0 : unlockLevel
            };
        }
    }
}
