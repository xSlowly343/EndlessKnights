using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Content.Weapons;
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
        private WeaponCatalog _weapons;

        public SkillBook(SkillBookRules rules, WeaponCatalog weapons, GameState state, SkillSystem skills, StatSheet hero)
        {
            _rules = rules;
            _weapons = weapons;
            _state = state;
            _skills = skills;
            _hero = hero;
        }

        public double ClassAttribute => _hero.Value(HeroAttributes.Stat, 0);

        public void UseRules(SkillBookRules rules) => _rules = rules;

        public void UseWeapons(WeaponCatalog weapons) => _weapons = weapons;

        public static bool IsClassSkill(GemDef gem) => gem.Kind == GemKind.Skill && gem.Skill?.Weapon != null && gem.Ascendancy == null;

        public int UnlockLevel(GemDef skill) => IsClassSkill(skill) ? _rules.SkillUnlockLevel(skill.Skill!.Tier) : 1;

        public bool OfHeroClass(GemDef skill) =>
            _state.ClassChosen && _weapons.TryGetType(skill.Skill?.Weapon ?? string.Empty, out var type) && _state.Is(type.HeroClass);

        public bool OpenedByWeapon(GemDef skill) => _state.Weapons.Opens(skill.Id);

        public bool SkillOpen(GemDef skill) =>
            IsClassSkill(skill) && !skill.Locked && (OfHeroClass(skill) || OpenedByWeapon(skill)) && _state.HeroLevel >= UnlockLevel(skill);

        public void ClassSkills(List<GemDef> into)
        {
            into.Clear();
            if (!_state.ClassChosen || !_weapons.HasType(_state.Class))
                return;
            into.AddRange(_weapons.PoolOf(_weapons.TypeOf(_state.Class)));
            var held = _state.Weapons.Held;
            if (held == null)
                return;
            foreach (var id in held.OpensSkills)
            {
                if (_weapons.TryGetPoolSkill(id, out var skill) && !into.Contains(skill))
                    into.Add(skill);
            }
        }

        public void KeepOpen()
        {
            for (var index = _state.Skills.Count - 1; index >= 0; index--)
            {
                var skill = _state.Skills[index].Skill;
                if (IsClassSkill(skill) && !OfHeroClass(skill) && !OpenedByWeapon(skill))
                    _skills.Unequip(index);
            }
        }

        public void OpenClassSkills(List<GemDef> into)
        {
            ClassSkills(into);
            into.RemoveAll(skill => !SkillOpen(skill));
        }

        public int SlotCount => _rules.SlotsFor(_state.RecordStage, _state.EarlyBookSlot);
        public int MaxSlots => _rules.MaxSlots;
        public int AutoSlotCount => System.Math.Max(0, SlotCount - SkillLoadout.HandSlots);
        public int AuraSlotCount => System.Math.Min(SkillLoadout.AuraSlots, _rules.AuraSlotsFor(_state.HeroLevel) + ExtraAuraSlots);

        private int ExtraAuraSlots => (int)System.Math.Floor(System.Math.Max(0, _hero.Value(CombatStats.ExtraAuraSlots, 0)) + 1e-9);

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

        public int MaxSockets => _rules.MaxSockets;

        public int SocketsAt(int index)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return 0;
            var slot = _state.Skills[index];
            var sockets = _rules.SocketsFor(_state.RecordStage, RebirthsOf(slot.Skill)) + ExtraSockets(slot.Role);
            return System.Math.Min(SkillSlot.BuiltInSocket, sockets);
        }

        private int ExtraSockets(SkillSlotRole role)
        {
            var stat = role switch
            {
                SkillSlotRole.Left => CombatStats.SocketsLeft,
                SkillSlotRole.Right => CombatStats.SocketsRight,
                SkillSlotRole.Auto => CombatStats.SocketsAuto,
                _ => (StatId?)null
            };
            return stat == null ? 0 : (int)System.Math.Floor(System.Math.Max(0, _hero.Value(stat.Value, 0)) + 1e-9);
        }

        public void TrimSockets()
        {
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var supports = _state.Skills[index].Supports;
                for (var socket = SocketsAt(index); socket < SkillSlot.BuiltInSocket; socket++)
                    supports[socket] = null;
            }
        }

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

        public bool SupportOpen(GemDef support) =>
            support.Type == SkillType.Support && _state.HeroLevel >= support.UnlockLevel && (support.ForPath == null || support.ForPath == _state.Ascendancy.Subclass);

        public void OpenSupports(IReadOnlyList<GemDef> catalog, List<GemDef> into)
        {
            into.Clear();
            for (var i = 0; i < catalog.Count; i++)
            {
                if (SupportOpen(catalog[i]))
                    into.Add(catalog[i]);
            }
        }

        public void SupportsFor(int index, IReadOnlyList<GemDef> catalog, List<GemDef> into)
        {
            into.Clear();
            if (index < 0 || index >= _state.Skills.Count)
                return;
            var skill = _state.Skills[index].Skill;
            for (var i = 0; i < catalog.Count; i++)
            {
                var support = catalog[i];
                if (support.Support != null && SupportOpen(support) && support.CanSupport(skill))
                    into.Add(support);
            }
        }

        public static bool OnSkill(SkillSlot slot, GemDef support)
        {
            for (var i = 0; i < SkillSlot.BuiltInSocket; i++)
            {
                if (ReferenceEquals(slot.Supports[i], support))
                    return true;
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
            if (support.Socket > 0 && socket != support.Socket - 1)
                return BookResult.WrongSocket;
            if (OnSkill(slot, support))
                return BookResult.AlreadyOnSkill;
            if (!SupportOpen(support))
                return BookResult.LevelTooLow;
            slot.Supports[socket] = support;
            _state.ProgressOf(support);
            return BookResult.Done;
        }

        public int FreeCopies(GemDef gem)
        {
            if (IsClassSkill(gem))
                return SkillOpen(gem) && RowOf(gem) < 0 ? 1 : 0;
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
                for (var i = 0; i < SkillSlot.BuiltInSocket; i++)
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
            if (skill.Locked)
                return BookResult.WaitsForMechanic;
            if (IsClassSkill(skill) && !OfHeroClass(skill) && !OpenedByWeapon(skill))
                return BookResult.WrongClass;
            if (IsClassSkill(skill) && _state.HeroLevel < UnlockLevel(skill))
                return BookResult.LevelTooLow;
            if (!IsClassSkill(skill) && FreeCopies(skill) <= 0)
                return BookResult.NotOwned;
            if (skill.Ascendancy != null && skill.Ascendancy != _state.Ascendancy.Subclass)
                return BookResult.WrongPath;
            if (RowOf(skill) >= 0)
                return BookResult.AlreadyInBook;
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

        public bool GrantStartingGems()
        {
            if (_state.OwnedGems.Count > 0)
                return false;
            foreach (var gem in _rules.StartingGems(_state.ClassName))
            {
                if (!IsClassSkill(gem))
                    _state.OwnedGems.Add(gem);
                if (gem.Kind == GemKind.Skill)
                    Equip(gem);
            }
            return true;
        }

        public void GrantClassGems()
        {
            var placed = 0;
            foreach (var gem in _rules.StartingGems(_state.ClassName))
            {
                if (IsClassSkill(gem))
                {
                    if (RowOf(gem) < 0)
                        placed += PlaceClassSkill(gem, placed);
                    continue;
                }
                if (_state.OwnedGems.Contains(gem))
                    continue;
                _state.OwnedGems.Add(gem);
                if (gem.Kind == GemKind.Skill)
                    placed += PlaceClassSkill(gem, placed);
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

        public BookResult Move(int from, int to)
        {
            if (from < 0 || from >= _state.Skills.Count || to < 0 || to >= _state.Skills.Count)
                return BookResult.NoSuchSlot;
            _skills.Move(from, to);
            return BookResult.Done;
        }
    }
}
