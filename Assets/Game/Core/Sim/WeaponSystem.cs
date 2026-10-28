using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class WeaponSystem
    {
        private readonly GameState _state;
        private readonly SkillSystem _skills;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private WeaponCatalog _catalog;
        private double _appliedScale = 1;

        public WeaponSystem(WeaponCatalog catalog, GameState state, StatSheet hero, SkillSystem skills)
        {
            _catalog = catalog;
            _state = state;
            _skills = skills;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            GiveStarter();
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public WeaponCatalog Catalog => _catalog;
        public WeaponItem? In(WeaponHand hand) => _state.Weapons.In(hand);
        public IReadOnlyList<WeaponItem> Bag => _state.Weapons.Bag;

        public void UseCatalog(WeaponCatalog catalog)
        {
            _catalog = catalog;
            var weapons = _state.Weapons;
            weapons.Right = Rebind(weapons.Right);
            weapons.Left = Rebind(weapons.Left);
            for (var i = weapons.Bag.Count - 1; i >= 0; i--)
            {
                var weapon = Rebind(weapons.Bag[i]);
                if (weapon == null)
                    weapons.Bag.RemoveAt(i);
                else
                    weapons.Bag[i] = weapon;
            }
            Changed();
        }

        public WeaponItem Give(WeaponBase weaponBase) => Give(_catalog.Starter(weaponBase));

        public WeaponItem Give(WeaponBase weaponBase, ItemRarity rarity, Rng rng) => Give(_catalog.Roll(weaponBase, rarity, rng));

        public WeaponItem Give(WeaponItem weapon)
        {
            _state.Weapons.Bag.Add(weapon);
            return weapon;
        }

        public bool Equip(WeaponHand hand, WeaponItem weapon)
        {
            var weapons = _state.Weapons;
            if (!weapons.Bag.Remove(weapon))
                return false;
            var previous = weapons.In(hand);
            if (previous != null)
                weapons.Bag.Add(previous);
            weapons.Set(hand, weapon);
            Changed();
            return true;
        }

        public bool Unequip(WeaponHand hand)
        {
            var weapons = _state.Weapons;
            var weapon = weapons.In(hand);
            if (weapon == null)
                return false;
            weapons.Set(hand, null);
            weapons.Bag.Add(weapon);
            Changed();
            return true;
        }

        private void GiveStarter()
        {
            var weapons = _state.Weapons;
            if (weapons.StarterGiven)
                return;
            weapons.StarterGiven = true;
            if (weapons.Right == null && weapons.Left == null)
                weapons.Right = _catalog.Starter(_catalog.StarterFor(_state.Class));
        }

        private WeaponItem? Rebind(WeaponItem? weapon) =>
            weapon != null && _catalog.TryGetBase(weapon.Id, out var found) ? weapon.Rebased(found) : null;

        private void Changed()
        {
            Rebuild();
            _skills.RefreshArmed();
        }

        public void RefreshInnate()
        {
            if (System.Math.Abs(InnateScale - _appliedScale) > 1e-12)
                Rebuild();
        }

        private double InnateScale => System.Math.Max(0, _hero.Value(CombatStats.WeaponInnateEffect, 1));

        private void Rebuild()
        {
            var modifiers = new List<Modifier>();
            _appliedScale = InnateScale;
            Add(modifiers, _state.Weapons.Right, _appliedScale);
            Add(modifiers, _state.Weapons.Left, _appliedScale);
            _layer.Set(modifiers);
        }

        private static void Add(List<Modifier> modifiers, WeaponItem? weapon, double scale)
        {
            if (weapon == null)
                return;
            AddScaled(modifiers, weapon.Type.Innate, scale);
            AddScaled(modifiers, weapon.Innate, scale);
            if (weapon.SecondType != null)
                AddScaled(modifiers, weapon.SecondType.Innate, scale);
            if (weapon.Unique != null)
                modifiers.AddRange(weapon.Unique.Lines);
        }

        private static void AddScaled(List<Modifier> modifiers, IReadOnlyList<Modifier> innate, double scale)
        {
            foreach (var modifier in innate)
                modifiers.Add(scale == 1 ? modifier : new Modifier(modifier.Stat, modifier.Kind, modifier.Value * scale, modifier.Tags));
        }
    }
}
