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
        private readonly ModifierLayer _layer;
        private WeaponCatalog _catalog;

        public WeaponSystem(WeaponCatalog catalog, GameState state, StatSheet hero, SkillSystem skills)
        {
            _catalog = catalog;
            _state = state;
            _skills = skills;
            _layer = new ModifierLayer(hero);
            GiveStarter();
            Rebuild();
        }

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

        private void Rebuild()
        {
            var modifiers = new List<Modifier>();
            Add(modifiers, _state.Weapons.Right);
            Add(modifiers, _state.Weapons.Left);
            _layer.Set(modifiers);
        }

        private static void Add(List<Modifier> modifiers, WeaponItem? weapon)
        {
            if (weapon == null)
                return;
            modifiers.AddRange(weapon.Type.Innate);
            modifiers.AddRange(weapon.Innate);
        }
    }
}
