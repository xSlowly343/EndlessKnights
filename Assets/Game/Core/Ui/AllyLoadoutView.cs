using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Party.Wire;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public static class AllyLoadoutView
    {
        public static void Fill(in PartyLoadoutMessage loadout, GemCatalog gems, AllySkillPlace[] into)
        {
            for (var position = 0; position < SkillLoadout.MaxSlots; position++)
            {
                var skill = loadout.At(position);
                into[position] = new AllySkillPlace
                {
                    Position = position,
                    Role = RoleAt(position),
                    Skill = !skill.Empty && gems.TryGetByNetId(skill.GemNetId, out var gem) ? gem : null,
                    Level = skill.Level,
                    Combo = skill.Combo
                };
            }
        }

        public static SkillSlotRole RoleAt(int position) => position switch
        {
            0 => SkillSlotRole.Left,
            1 => SkillSlotRole.Right,
            _ => position < SkillLoadout.HandSlots + SkillLoadout.AutoSlots ? SkillSlotRole.Auto : SkillSlotRole.Aura
        };

        public static string Branches(in AllySkillPlace place)
        {
            if (place.Skill == null || place.Skill.Branches.Count == 0)
                return string.Empty;
            var names = new List<string>(SkillBranchTier.Count);
            for (var tier = 0; tier < place.Skill.Branches.Count; tier++)
            {
                var choice = SkillBranches.Choice(place.Combo, tier);
                names.Add(choice == SkillBranches.NoChoice ? "—" : BranchTexts.Name(place.Skill.Branches[tier].Options[choice]));
            }
            return string.Join(" · ", names);
        }
    }
}
