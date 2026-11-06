using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class WeaponSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private WeaponCatalog _catalog;
        private double _appliedScale = 1;

        public WeaponSystem(WeaponCatalog catalog, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            GiveStarter();
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
        public System.Action? HeldChanged { get; set; }

        public WeaponCatalog Catalog => _catalog;
        public WeaponItem? Held => _state.Weapons.Held;
        public IReadOnlyList<WeaponItem> Bag => _state.Weapons.Bag;

        public void UseCatalog(WeaponCatalog catalog)
        {
            _catalog = catalog;
            var weapons = _state.Weapons;
            weapons.Held = Rebind(weapons.Held);
            for (var i = weapons.Bag.Count - 1; i >= 0; i--)
            {
                var weapon = Rebind(weapons.Bag[i]);
                if (weapon == null)
                    weapons.Bag.RemoveAt(i);
                else
                    weapons.Bag[i] = weapon;
            }
            Rebuild();
        }

        public WeaponItem Give(WeaponBase weaponBase) => Give(_catalog.Starter(weaponBase));

        public WeaponItem Give(WeaponBase weaponBase, ItemRarity rarity) => Give(_catalog.Roll(weaponBase, rarity));

        public WeaponItem Give(WeaponItem weapon)
        {
            _state.Weapons.Bag.Add(weapon);
            return weapon;
        }

        public bool Fits(WeaponItem weapon) => !_state.ClassChosen || _state.Is(weapon.Type.HeroClass);

        public bool Equip(WeaponItem weapon)
        {
            var weapons = _state.Weapons;
            if (!Fits(weapon) || !weapons.Bag.Remove(weapon))
                return false;
            if (weapons.Held != null)
                weapons.Bag.Add(weapons.Held);
            weapons.Held = weapon;
            Rebuild();
            HeldChanged?.Invoke();
            return true;
        }

        public bool Owns(WeaponItem weapon) => _state.Weapons.Held == weapon || _state.Weapons.Bag.Contains(weapon);

        public bool Replace(WeaponItem old, WeaponItem fresh)
        {
            var weapons = _state.Weapons;
            var bag = weapons.Bag.IndexOf(old);
            if (bag >= 0)
            {
                weapons.Bag[bag] = fresh;
                return true;
            }
            if (weapons.Held != old)
                return false;
            weapons.Held = fresh;
            Rebuild();
            HeldChanged?.Invoke();
            return true;
        }

        public bool Unequip()
        {
            var weapons = _state.Weapons;
            if (weapons.Held == null)
                return false;
            weapons.Bag.Add(weapons.Held);
            weapons.Held = null;
            Rebuild();
            HeldChanged?.Invoke();
            return true;
        }

        public void Reforge(WeaponType from, WeaponType to)
        {
            var weapons = _state.Weapons;
            for (var i = 0; i < weapons.Bag.Count; i++)
                weapons.Bag[i] = Reforged(weapons.Bag[i], from, to)!;
            weapons.Held = Reforged(weapons.Held, from, to);
            if (weapons.Held != null && weapons.Held.Type != to)
            {
                weapons.Bag.Add(weapons.Held);
                weapons.Held = null;
            }
            Rebuild();
        }

        private WeaponItem? Reforged(WeaponItem? weapon, WeaponType from, WeaponType to) =>
            weapon != null && weapon.Type == from ? _catalog.Reforged(weapon, to) : weapon;

        public void GiveClassStarter()
        {
            GiveStarter();
            ReforgeBagToClass();
            Rebuild();
        }

        private void ReforgeBagToClass()
        {
            if (!_state.ClassChosen || !_catalog.HasType(_state.Class))
                return;
            var own = _catalog.TypeOf(_state.Class);
            var bag = _state.Weapons.Bag;
            for (var i = 0; i < bag.Count; i++)
            {
                if (bag[i].Type != own)
                    bag[i] = _catalog.Reforged(bag[i], own);
            }
        }

        private void GiveStarter()
        {
            var weapons = _state.Weapons;
            if (weapons.StarterGiven || !_state.ClassChosen || !_catalog.HasType(_state.Class))
                return;
            weapons.StarterGiven = true;
            if (weapons.Held == null)
                weapons.Held = _catalog.Starter(_catalog.StarterFor(_state.Class));
        }

        private WeaponItem? Rebind(WeaponItem? weapon) =>
            weapon != null && _catalog.TryGetBase(weapon.Id, out var found) ? weapon.Rebased(found) : null;

        public void RefreshInnate()
        {
            if (System.Math.Abs(InnateScale - _appliedScale) > 1e-12)
                Rebuild();
        }

        public void ApplyBuiltInSupports(List<SkillSlot> skills)
        {
            var held = _state.Weapons.Held;
            for (var i = 0; i < skills.Count; i++)
            {
                var slot = skills[i];
                var support = held?.BuiltInSupport(slot.Role);
                slot.Supports[SkillSlot.BuiltInSocket] = support != null && support.CanSupport(slot.Skill) ? support : null;
            }
        }

        private double InnateScale => System.Math.Max(0, _hero.Value(CombatStats.WeaponInnateEffect, 1));

        public void Refresh() => Rebuild();

        private void Rebuild()
        {
            var modifiers = new List<Modifier>();
            _appliedScale = InnateScale;
            var weapon = _state.Weapons.Held;
            if (weapon != null)
            {
                AddScaled(modifiers, weapon.Type.Innate, _appliedScale);
                AddScaled(modifiers, weapon.Innate, _appliedScale * weapon.Forge.Scale);
                var start = modifiers.Count;
                foreach (var affix in weapon.Affixes)
                    affix.AddModifiers(modifiers);
                GearItem.Scale(modifiers, start, weapon.Forge.AffixScale);
                if (weapon.Unique != null)
                    modifiers.AddRange(weapon.Unique.Lines);
                if (weapon.Set != null)
                    modifiers.AddRange(weapon.Set.PieceLines(ItemSlot.Weapon));
            }
            _layer.Set(modifiers);
        }

        private static void AddScaled(List<Modifier> modifiers, IReadOnlyList<Modifier> innate, double scale)
        {
            foreach (var modifier in innate)
                modifiers.Add(scale == 1 ? modifier : modifier.WithValue(modifier.Value * scale));
        }
    }
}
