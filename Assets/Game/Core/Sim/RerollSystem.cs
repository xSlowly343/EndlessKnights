using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public sealed class RerollSystem
    {
        private const double DustEpsilon = 1e-9;

        private readonly GameState _state;
        private readonly GearSystem _gear;
        private readonly WeaponSystem _weapons;
        private DropRules _rules;

        public RerollSystem(DropRules rules, GameState state, GearSystem gear, WeaponSystem weapons)
        {
            _rules = rules;
            _state = state;
            _gear = gear;
            _weapons = weapons;
        }

        public RerollOffer? Offer { get; private set; }

        public void UseRules(DropRules rules) => _rules = rules;

        public double Cost(ItemRarity rarity, bool locked) => _rules.RerollCost(rarity, locked);

        public RerollResult Check(WeaponItem weapon, int? locked = null) =>
            !_weapons.Owns(weapon) ? RerollResult.NotOwned : Check(weapon.Unique != null, weapon.Affixes, weapon.Rarity, locked);

        public RerollResult Check(GearItem item, int? locked = null) =>
            !_gear.Owns(item) ? RerollResult.NotOwned : Check(item.Unique != null || item.Base == null, item.Affixes, item.Rarity, locked);

        public RerollResult Reroll(WeaponItem weapon, int? locked = null)
        {
            var result = Check(weapon, locked);
            if (result == RerollResult.Offered)
                Offer = Rolled(weapon, null, weapon.Affixes, weapon.Rarity, locked);
            return result;
        }

        public RerollResult Reroll(GearItem item, int? locked = null)
        {
            var result = Check(item, locked);
            if (result == RerollResult.Offered)
                Offer = Rolled(null, item, item.Affixes, item.Rarity, locked);
            return result;
        }

        public bool TakeNew()
        {
            var offer = Offer;
            Offer = null;
            if (offer == null)
                return false;
            if (offer.Weapon != null)
                return _weapons.Replace(offer.Weapon, offer.Weapon.WithAffixes(offer.After));
            return offer.Gear != null && _gear.Replace(offer.Gear, offer.Gear.WithAffixes(offer.After));
        }

        public void KeepOld() => Offer = null;

        private RerollResult Check(bool fixedLines, IReadOnlyList<GearAffix> affixes, ItemRarity rarity, int? locked)
        {
            if (Offer != null)
                return RerollResult.Pending;
            if (fixedLines)
                return RerollResult.Unique;
            if (locked != null && (locked < 0 || locked >= affixes.Count))
                return RerollResult.BadLock;
            if (!AffixReroll.CanReroll(affixes, locked))
                return RerollResult.NothingToRoll;
            return _state.Drops.Dust + DustEpsilon < Cost(rarity, locked != null) ? RerollResult.NotEnoughDust : RerollResult.Offered;
        }

        private RerollOffer Rolled(WeaponItem? weapon, GearItem? gear, IReadOnlyList<GearAffix> affixes, ItemRarity rarity, int? locked)
        {
            var cost = Cost(rarity, locked != null);
            _state.Drops.Dust = System.Math.Max(0, _state.Drops.Dust - cost);
            return new RerollOffer(weapon, gear, affixes, AffixReroll.Rerolled(affixes, locked, _state.Drops.Rng), locked, cost);
        }
    }
}
