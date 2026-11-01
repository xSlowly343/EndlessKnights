using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;

namespace Game.Core.Sim.Headless
{
    public sealed class BotLoadout
    {
        private static readonly SkillSlotRole[] ActiveOrder = { SkillSlotRole.Auto, SkillSlotRole.Left, SkillSlotRole.Right };

        private readonly GameLoop _loop;
        private readonly List<GemDef> _candidates = new List<GemDef>();
        private readonly List<(GemDef Skill, SkillSlotRole Role)> _plan = new List<(GemDef, SkillSlotRole)>();

        public BotLoadout(GameLoop loop)
        {
            _loop = loop;
        }

        public void Arrange()
        {
            CollectCandidates();
            Plan();
            if (!Matches())
                Rebuild();
        }

        private void CollectCandidates()
        {
            _candidates.Clear();
            var state = _loop.State;
            AddWeaponSkills(state.Weapons.Right);
            AddWeaponSkills(state.Weapons.Left);
            foreach (var gem in state.OwnedGems)
            {
                if (gem.Kind == GemKind.Skill && Usable(gem) && _loop.Book.FreeCopies(gem) > 0)
                    Add(gem);
            }
        }

        private void AddWeaponSkills(Content.Weapons.WeaponItem? weapon)
        {
            if (weapon == null)
                return;
            var unlockLevels = _loop.Data.Weapons.SkillRules.UnlockLevels;
            foreach (var id in weapon.Skills)
            {
                if (_loop.Data.Gems.TryGet(id, out var skill) && Usable(skill) && _loop.State.Weapons.Opens(id, _loop.State.HeroLevel, unlockLevels))
                    Add(skill);
            }
        }

        private bool Usable(GemDef skill) =>
            !skill.Locked && (skill.Ascendancy == null || skill.Ascendancy == _loop.State.Ascendancy.Subclass);

        private void Add(GemDef skill)
        {
            if (!_candidates.Contains(skill))
                _candidates.Add(skill);
        }

        private void Plan()
        {
            _plan.Clear();
            var book = _loop.Book;
            var auraRoom = book.AuraSlotCount;
            var activeRoom = new Dictionary<SkillSlotRole, int>
            {
                [SkillSlotRole.Auto] = book.AutoSlotCount,
                [SkillSlotRole.Left] = 1,
                [SkillSlotRole.Right] = 1
            };
            var spareAuras = new List<GemDef>();
            foreach (var skill in _candidates)
            {
                if (SkillLoadout.IsAura(skill))
                {
                    if (auraRoom > 0)
                    {
                        _plan.Add((skill, SkillSlotRole.Aura));
                        auraRoom--;
                    }
                    else
                    {
                        spareAuras.Add(skill);
                    }
                }
                else if (FreeRole(activeRoom) is { } role)
                {
                    _plan.Add((skill, role));
                    activeRoom[role]--;
                }
            }
            foreach (var aura in spareAuras)
            {
                if (FreeRole(activeRoom) is not { } role)
                    break;
                _plan.Add((aura, role));
                activeRoom[role]--;
            }
        }

        private static SkillSlotRole? FreeRole(Dictionary<SkillSlotRole, int> room)
        {
            foreach (var role in ActiveOrder)
            {
                if (room[role] > 0)
                    return role;
            }
            return null;
        }

        private bool Matches()
        {
            var skills = _loop.State.Skills;
            if (skills.Count != _plan.Count)
                return false;
            foreach (var (skill, role) in _plan)
            {
                var row = SkillLoadout.RowOf(skills, skill);
                if (row == SkillLoadout.NoSlot || skills[row].Role != role)
                    return false;
            }
            return true;
        }

        private void Rebuild()
        {
            var book = _loop.Book;
            for (var row = _loop.State.Skills.Count - 1; row >= 0; row--)
            {
                if (!Planned(_loop.State.Skills[row].Skill))
                    book.Unequip(row);
            }
            foreach (var (skill, role) in _plan)
            {
                var row = book.RowOf(skill);
                if (row == SkillLoadout.NoSlot)
                {
                    if (Place(skill) != BookResult.Done)
                        continue;
                    row = book.RowOf(skill);
                }
                if (_loop.State.Skills[row].Role != role && book.Assign(row, role) != BookResult.Done)
                    MakeRoom(row, role);
            }
        }

        private BookResult Place(GemDef skill)
        {
            var result = _loop.Book.PlaceFromWeapon(skill, _loop.Data.Weapons.SkillRules.UnlockLevels);
            return result == BookResult.Done ? result : _loop.Book.Equip(skill);
        }

        private void MakeRoom(int row, SkillSlotRole role)
        {
            var skills = _loop.State.Skills;
            for (var other = 0; other < skills.Count; other++)
            {
                if (other == row || skills[other].Role != role || PlannedRole(skills[other].Skill) == role)
                    continue;
                var displaced = skills[other].Role;
                skills[other].Role = skills[row].Role;
                skills[row].Role = displaced;
                return;
            }
        }

        private bool Planned(GemDef skill)
        {
            foreach (var entry in _plan)
            {
                if (ReferenceEquals(entry.Skill, skill))
                    return true;
            }
            return false;
        }

        private SkillSlotRole? PlannedRole(GemDef skill)
        {
            foreach (var entry in _plan)
            {
                if (ReferenceEquals(entry.Skill, skill))
                    return entry.Role;
            }
            return null;
        }
    }
}
