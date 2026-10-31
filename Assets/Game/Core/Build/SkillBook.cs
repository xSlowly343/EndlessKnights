using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public sealed class SkillBook
    {
        private readonly GameState _state;
        private readonly SkillSystem _skills;
        private readonly StatSheet _hero;
        private SkillBookRules _rules;

        public SkillBook(SkillBookRules rules, GameState state, SkillSystem skills, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _skills = skills;
            _hero = hero;
        }

        public double Attribute(string attribute) => _hero.Value(HeroAttributes.Stat(attribute), 0);

        public bool MeetsRequirements(GemDef gem)
        {
            foreach (var requirement in gem.Requires)
            {
                if (Attribute(requirement.Key) < requirement.Value)
                    return false;
            }
            return true;
        }

        public void UseRules(SkillBookRules rules) => _rules = rules;

        public int SlotCount => _rules.SlotsFor(_state.RecordStage, _state.EarlyBookSlot);
        public int MaxSlots => _rules.MaxSlots;
        public int AutoSlotCount => System.Math.Max(0, SlotCount - SkillLoadout.HandSlots);
        public int AuraSlotCount => _rules.AuraSlotsFor(_state.HeroLevel);

        public int AuraSlotLevel(int slot) => _rules.AuraSlotLevel(slot);

        public SkillSlotRole? FreeRoleFor(GemDef skill) => SkillLoadout.FreeRole(_state.Skills, skill, AutoSlotCount, AuraSlotCount);

        public bool HasRoomFor(GemDef skill) => FreeRoleFor(skill) != null;

        public int Capacity(SkillSlotRole role) => SkillLoadout.Capacity(role, AutoSlotCount, AuraSlotCount);

        public BookResult Assign(int index, SkillSlotRole role)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            var slot = _state.Skills[index];
            if (slot.Role == role)
                return BookResult.Done;
            if (!SkillLoadout.Accepts(role, slot.Skill))
                return BookResult.WrongSlot;
            if (SkillLoadout.HasRoom(_state.Skills, role, AutoSlotCount, AuraSlotCount))
            {
                slot.Role = role;
                return BookResult.Done;
            }
            if (role != SkillSlotRole.Left && role != SkillSlotRole.Right)
                return BookResult.NoFreeSlot;
            var occupant = _state.Skills[SkillLoadout.IndexOf(_state.Skills, role)];
            if (!SkillLoadout.Accepts(slot.Role, occupant.Skill))
                return BookResult.WrongSlot;
            occupant.Role = slot.Role;
            slot.Role = role;
            return BookResult.Done;
        }

        public long UnlockStageOf(int slot) => _rules.UnlockStageOf(slot, _state.EarlyBookSlot);

        public int SocketCount => _rules.SocketsFor(_state.RecordStage, 0);

        public int SocketsAt(int index) =>
            index < 0 || index >= _state.Skills.Count ? 0 : _rules.SocketsFor(_state.RecordStage, RebirthsOf(_state.Skills[index].Skill));

        public int RebirthsOf(GemDef gem) => _state.GemProgress.TryGetValue(gem.Id, out var progress) ? progress.Rebirths : 0;

        public bool CanRebirth(GemDef gem, int maxLevel) =>
            _state.GemProgress.TryGetValue(gem.Id, out var progress) && progress.Level >= maxLevel && progress.Rebirths < _rules.SocketRebirths;

        public bool Rebirth(GemDef gem, int maxLevel)
        {
            if (!CanRebirth(gem, maxLevel))
                return false;
            var progress = _state.GemProgress[gem.Id];
            progress.Rebirths++;
            progress.Level = 1;
            progress.Experience = 0;
            _skills.RefreshBranches();
            return true;
        }

        public bool SupportOpen(GemDef support) => support.Type == SkillType.Support && _state.HeroLevel >= support.UnlockLevel;

        public void OpenSupports(IReadOnlyList<GemDef> catalog, List<GemDef> into)
        {
            into.Clear();
            for (var i = 0; i < catalog.Count; i++)
            {
                if (SupportOpen(catalog[i]))
                    into.Add(catalog[i]);
            }
        }

        public bool InBuild(GemDef support)
        {
            foreach (var slot in _state.Skills)
            {
                for (var i = 0; i < slot.Supports.Length; i++)
                {
                    if (ReferenceEquals(slot.Supports[i], support))
                        return true;
                }
            }
            return false;
        }

        public BookResult SetSupport(int index, int socket, GemDef? support)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            if (socket < 0 || socket >= SocketsAt(index))
                return BookResult.NoSuchSocket;
            var slot = _state.Skills[index];
            if (support == null)
            {
                slot.Supports[socket] = null;
                return BookResult.Done;
            }
            if (support.Kind != GemKind.Support)
                return BookResult.WrongKind;
            if (support.Support == null)
                return BookResult.WaitsForMechanic;
            if (ReferenceEquals(slot.Supports[socket], support))
                return BookResult.Done;
            if (!support.CanSupport(slot.Skill))
                return BookResult.DoesNotFit;
            if (InBuild(support))
                return BookResult.AlreadyInBuild;
            if (!SupportOpen(support))
                return BookResult.LevelTooLow;
            slot.Supports[socket] = support;
            _state.ProgressOf(support);
            return BookResult.Done;
        }

        public int FreeCopies(GemDef gem)
        {
            var owned = 0;
            foreach (var item in _state.OwnedGems)
            {
                if (ReferenceEquals(item, gem))
                    owned++;
            }
            foreach (var slot in _state.Skills)
            {
                if (ReferenceEquals(slot.Skill, gem))
                    owned--;
                if (ReferenceEquals(slot.Trigger, gem))
                    owned--;
                for (var i = 0; i < slot.Supports.Length; i++)
                {
                    if (ReferenceEquals(slot.Supports[i], gem))
                        owned--;
                }
            }
            return owned;
        }

        public BookResult Equip(GemDef skill)
        {
            if (skill.Kind != GemKind.Skill)
                return BookResult.WrongKind;
            if (FreeCopies(skill) <= 0)
                return BookResult.NotOwned;
            if (skill.Locked)
                return BookResult.WaitsForMechanic;
            if (skill.Ascendancy != null && skill.Ascendancy != _state.Ascendancy.Subclass)
                return BookResult.WrongPath;
            if (RowOf(skill) >= 0)
                return BookResult.AlreadyInBook;
            if (!MeetsRequirements(skill))
                return BookResult.AttributesTooLow;
            var role = FreeRoleFor(skill);
            if (role == null)
                return BookResult.NoFreeSlot;
            _skills.Equip(skill, role: role);
            return BookResult.Done;
        }

        public BookResult PlaceFromWeapon(GemDef skill, IReadOnlyList<int> unlockLevels)
        {
            if (skill.Kind != GemKind.Skill)
                return BookResult.WrongKind;
            if (skill.Locked)
                return BookResult.WaitsForMechanic;
            if (!_state.Weapons.Opens(skill.Id, _state.HeroLevel, unlockLevels))
                return BookResult.NotOnWeapon;
            if (RowOf(skill) >= 0)
                return BookResult.AlreadyInBook;
            if (!MeetsRequirements(skill))
                return BookResult.AttributesTooLow;
            var role = FreeRoleFor(skill);
            if (role == null)
                return BookResult.NoFreeSlot;
            _skills.Equip(skill, role: role);
            return BookResult.Done;
        }

        public int RowOf(GemDef skill) => SkillLoadout.RowOf(_state.Skills, skill);

        public BookResult Unequip(int index)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            _skills.Unequip(index);
            return BookResult.Done;
        }

        public BookResult SetTrigger(int index, GemDef? trigger)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            if (trigger != null && trigger.Kind != GemKind.Trigger)
                return BookResult.WrongKind;
            var slot = _state.Skills[index];
            if (trigger != null && !ReferenceEquals(slot.Trigger, trigger) && FreeCopies(trigger) <= 0)
                return BookResult.NotOwned;
            if (trigger != null && !MeetsRequirements(trigger))
                return BookResult.AttributesTooLow;
            slot.Trigger = trigger;
            if (trigger != null)
                _state.ProgressOf(trigger);
            return BookResult.Done;
        }

        public bool GrantStartingGems()
        {
            if (_state.OwnedGems.Count > 0)
                return false;
            _state.OwnedGems.AddRange(_rules.StartingGems(_state.ClassName));
            foreach (var gem in _state.OwnedGems)
            {
                if (gem.Kind == GemKind.Skill)
                    Equip(gem);
            }
            foreach (var gem in _state.OwnedGems)
            {
                if (gem.Kind == GemKind.Trigger)
                    AttachToFirstPaidSkill(gem);
            }
            return true;
        }

        public void GrantClassGems()
        {
            var placed = 0;
            foreach (var gem in _rules.StartingGems(_state.ClassName))
            {
                if (_state.OwnedGems.Contains(gem))
                    continue;
                _state.OwnedGems.Add(gem);
                if (gem.Kind == GemKind.Skill)
                    placed += PlaceClassSkill(gem, placed);
                else if (gem.Kind == GemKind.Trigger)
                    AttachToFirstPaidSkill(gem);
            }
        }

        private int PlaceClassSkill(GemDef skill, int position)
        {
            if (!HasRoomFor(skill))
                FreeGeneralSlot(SkillLoadout.IsAura(skill));
            if (Equip(skill) != BookResult.Done)
                return 0;
            Move(_state.Skills.Count - 1, position);
            return 1;
        }

        private void FreeGeneralSlot(bool aura)
        {
            for (var index = _state.Skills.Count - 1; index >= 0; index--)
            {
                if (_state.Skills[index].Def.Weapon == null && SkillLoadout.IsAura(_state.Skills[index].Skill) == aura)
                {
                    Unequip(index);
                    return;
                }
            }
        }

        private void AttachToFirstPaidSkill(GemDef trigger)
        {
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                if (slot.Trigger == null && slot.Def.ResourceCost > 0)
                {
                    SetTrigger(index, trigger);
                    return;
                }
            }
        }

        public BookResult Move(int from, int to)
        {
            if (from < 0 || from >= _state.Skills.Count || to < 0 || to >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            _skills.Move(from, to);
            return BookResult.Done;
        }
    }
}
