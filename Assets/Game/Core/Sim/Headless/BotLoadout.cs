using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public sealed class BotLoadout
    {
        private static readonly SkillSlotRole[] ActiveOrder = { SkillSlotRole.Auto, SkillSlotRole.Left, SkillSlotRole.Right };

        private readonly GameLoop _loop;
        private readonly BranchTrial? _trial;
        private readonly List<GemDef> _candidates = new List<GemDef>();
        private readonly List<GemDef> _classSkills = new List<GemDef>();
        private readonly List<GemDef> _common = new List<GemDef>();
        private readonly List<GemDef> _order = new List<GemDef>();
        private readonly HashSet<GemDef> _seen = new HashSet<GemDef>();
        private readonly List<(GemDef Skill, SkillSlotRole Role)> _plan = new List<(GemDef, SkillSlotRole)>();

        public BotLoadout(GameLoop loop, BranchTrial? trial = null)
        {
            _loop = loop;
            _trial = trial;
        }

        public void Arrange()
        {
            UpdateOrder();
            CollectCandidates();
            Plan();
            if (!Matches())
                Rebuild();
        }

        private void UpdateOrder()
        {
            _loop.Book.OpenClassSkills(_classSkills);
            if (_order.Count > 0 && !_loop.Book.OfHeroClass(_order[0]))
            {
                _order.Clear();
                _seen.Clear();
            }
            if (_seen.Count == 0)
            {
                SeedFromBook();
                return;
            }
            foreach (var skill in _classSkills)
            {
                if (!_seen.Add(skill))
                    continue;
                if (_trial == null)
                    InsertByTier(_order, skill);
                else
                    TryByTrial(skill, _trial);
            }
        }

        private void SeedFromBook()
        {
            foreach (var slot in _loop.State.Skills)
            {
                if (_classSkills.Contains(slot.Skill) && _seen.Add(slot.Skill))
                    _order.Add(slot.Skill);
            }
            foreach (var skill in _classSkills)
            {
                if (_seen.Add(skill))
                    InsertByTier(_order, skill);
            }
        }

        private static void InsertByTier(List<GemDef> order, GemDef skill)
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (SkillLoadout.IsAura(order[i]) == SkillLoadout.IsAura(skill) && order[i].Skill!.Tier < skill.Skill!.Tier)
                {
                    order.Insert(i, skill);
                    return;
                }
            }
            order.Add(skill);
        }

        private void TryByTrial(GemDef skill, BranchTrial trial)
        {
            var best = new List<GemDef>(_order) { skill };
            var bestDamage = DamageWith(best, trial);
            var aura = SkillLoadout.IsAura(skill);
            var room = aura ? _loop.Book.AuraSlotCount : SkillLoadout.HandSlots + _loop.Book.AutoSlotCount;
            for (var i = 0; i < _order.Count && room > 0; i++)
            {
                if (SkillLoadout.IsAura(_order[i]) != aura)
                    continue;
                room--;
                var option = new List<GemDef>(_order);
                option.Insert(i, skill);
                var damage = DamageWith(option, trial);
                if (damage > bestDamage)
                {
                    best = option;
                    bestDamage = damage;
                }
            }
            _order.Clear();
            _order.AddRange(best);
        }

        private BigNumber DamageWith(List<GemDef> order, BranchTrial trial) =>
            trial.DamageAfter(_loop.State, loop =>
            {
                var loadout = new BotLoadout(loop);
                loadout._order.AddRange(order);
                loadout._seen.UnionWith(order);
                loop.Book.OpenClassSkills(loadout._classSkills);
                loadout._seen.UnionWith(loadout._classSkills);
                loadout.Arrange();
                return true;
            });

        private void CollectCandidates()
        {
            _candidates.Clear();
            AddClassSkills(aura: false, onlyFirst: false);
            AddClassSkills(aura: true, onlyFirst: true);
            foreach (var gem in _loop.State.OwnedGems)
            {
                if (gem.Kind == GemKind.Skill && Usable(gem) && _loop.Book.FreeCopies(gem) > 0)
                    Add(gem);
            }
            AddClassSkills(aura: true, onlyFirst: false);
            _loop.Book.OpenCommonSkills(_loop.Data.Gems.All, _common);
            foreach (var gem in _common)
            {
                if (Usable(gem) && _loop.Book.FreeCopies(gem) > 0)
                    Add(gem);
            }
        }

        private void AddClassSkills(bool aura, bool onlyFirst)
        {
            foreach (var skill in _order)
            {
                if (SkillLoadout.IsAura(skill) != aura || !_loop.Book.SkillOpen(skill))
                    continue;
                Add(skill);
                if (onlyFirst)
                    return;
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
            var spareActives = new List<GemDef>();
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
                else if (activeRoom[PreferredRole(skill)] > 0)
                {
                    _plan.Add((skill, PreferredRole(skill)));
                    activeRoom[PreferredRole(skill)]--;
                }
                else
                {
                    spareActives.Add(skill);
                }
            }
            foreach (var skill in spareActives)
            {
                if (FreeRole(activeRoom) is not { } role)
                    break;
                _plan.Add((skill, role));
                activeRoom[role]--;
            }
            foreach (var aura in spareAuras)
            {
                if (FreeRole(activeRoom) is not { } role)
                    break;
                _plan.Add((aura, role));
                activeRoom[role]--;
            }
        }

        private static SkillSlotRole PreferredRole(GemDef skill) => skill.Skill?.Role switch
        {
            SkillRole.Fast => SkillSlotRole.Left,
            SkillRole.Heavy => SkillSlotRole.Right,
            _ => SkillSlotRole.Auto
        };

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

        private BookResult Place(GemDef skill) => _loop.Book.Equip(skill);

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
