using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GearSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private ItemCatalog _catalog;

        public GearSystem(ItemCatalog catalog, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
        public System.Action? Rebuilt { get; set; }

        public void UseCatalog(ItemCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public bool Collected(SetDef set)
        {
            foreach (var piece in set.Pieces)
            {
                if (!_state.SetCollection.Contains(SetDef.PieceKey(set.Id, piece)))
                    return false;
            }
            return true;
        }

        public int CollectedPieces(SetDef set)
        {
            var count = 0;
            foreach (var piece in set.Pieces)
            {
                if (_state.SetCollection.Contains(SetDef.PieceKey(set.Id, piece)))
                    count++;
            }
            return count;
        }

        public bool NoteFound(GearItem item) => item.Set != null && NoteFound(item.Set, item.Slot, item.SecondRing);

        public bool NoteFound(SetDef set, ItemSlot slot, bool secondRing = false)
        {
            if (!_state.SetCollection.Add(SetDef.PieceKey(set.Id, slot, secondRing)))
                return false;
            if (Collected(set))
                Rebuild();
            return true;
        }
        public GearItem? In(GearSlot slot) => _state.Gear.In(slot);
        public IReadOnlyList<GearItem> Bag => _state.Drops.Gear;

        public bool Equip(GearSlot slot, GearItem item)
        {
            if (!item.Wearable || !GearState.Fits(slot, item.Slot) || !_state.Drops.Gear.Remove(item))
                return false;
            var previous = _state.Gear.Worn[(int)slot];
            if (previous != null)
                _state.Drops.Gear.Add(previous);
            _state.Gear.Worn[(int)slot] = item;
            Rebuild();
            return true;
        }

        public bool Equip(GearItem item) => Equip(FreeSlotFor(item), item);

        public bool Unequip(GearSlot slot)
        {
            var item = _state.Gear.Worn[(int)slot];
            if (item == null)
                return false;
            _state.Gear.Worn[(int)slot] = null;
            _state.Drops.Gear.Add(item);
            Rebuild();
            return true;
        }

        public bool Owns(GearItem item) => _state.Drops.Gear.Contains(item) || System.Array.IndexOf(_state.Gear.Worn, item) >= 0;

        public bool Replace(GearItem old, GearItem fresh)
        {
            var bag = _state.Drops.Gear.IndexOf(old);
            if (bag >= 0)
            {
                _state.Drops.Gear[bag] = fresh;
                return true;
            }
            var worn = System.Array.IndexOf(_state.Gear.Worn, old);
            if (worn < 0)
                return false;
            _state.Gear.Worn[worn] = fresh;
            Rebuild();
            return true;
        }

        public GearSlot FreeSlotFor(GearItem item)
        {
            if (item.Slot == ItemSlot.Ring && _state.Gear.In(GearSlot.RingLeft) != null && _state.Gear.In(GearSlot.RingRight) == null)
                return GearSlot.RingRight;
            return GearState.SlotFor(item.Slot);
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            foreach (var item in _state.Gear.Worn)
                item?.AddModifiers(modifiers);
            AddRingPair(modifiers);
            System.Array.Clear(_state.SetPerks, 0, _state.SetPerks.Length);
            System.Array.Clear(_collectedByRarity, 0, _collectedByRarity.Length);
            foreach (var set in _catalog.Sets)
            {
                if (Collected(set))
                    _collectedByRarity[(int)set.Rarity]++;
            }
            var scale = MasteryScale;
            foreach (var set in _catalog.Sets)
            {
                if (!Collected(set))
                    continue;
                AddScaled(modifiers, set.Bonus, scale);
                modifiers.AddRange(set.Primary);
                AddScaled(modifiers, set.PerRaritySet, scale * _collectedByRarity[(int)set.Rarity]);
                AddScaled(modifiers, set.PerMuseumCollection, scale * _state.Museum.Done.Count);
                AddScaled(modifiers, set.Echo, scale * AlliesEchoing(set));
                foreach (var perk in set.Perks)
                    _state.SetPerks[(int)perk.Perk] += perk.Value;
            }
            AddCollectionMore(modifiers);
            _layer.Set(modifiers);
            Rebuilt?.Invoke();
        }

        private readonly int[] _collectedByRarity = new int[System.Enum.GetValues(typeof(ItemRarity)).Length];

        public int MasteryLevel => _catalog.MasteryLevel(_state.Drops.CraftSpent);

        public double MasteryScale => _catalog.MasteryScale(MasteryLevel);

        public int CollectedSetsOf(ItemRarity rarity) => _collectedByRarity[(int)rarity];

        public uint EchoMask
        {
            get
            {
                var mask = 0u;
                foreach (var set in _catalog.Sets)
                {
                    if (set.EchoBit >= 0 && Collected(set))
                        mask |= 1u << set.EchoBit;
                }
                return mask;
            }
        }

        private int AlliesEchoing(SetDef set)
        {
            if (set.EchoBit < 0)
                return 0;
            var count = 0;
            foreach (var mask in _state.AllyEchoMasks)
            {
                if ((mask & (1u << set.EchoBit)) != 0)
                    count++;
            }
            return count;
        }

        public void SetAllyEcho(int slot, uint mask)
        {
            if (slot < 0 || slot >= _state.AllyEchoMasks.Length || _state.AllyEchoMasks[slot] == mask)
                return;
            _state.AllyEchoMasks[slot] = mask;
            Rebuild();
        }

        public double CollectionMore
        {
            get
            {
                var total = 1.0;
                for (var i = 0; i < _collectedByRarity.Length; i++)
                    total *= _catalog.CollectionMore((ItemRarity)i, _collectedByRarity[i]);
                return total;
            }
        }

        private void AddCollectionMore(List<Modifier> modifiers)
        {
            var total = CollectionMore;
            if (total > 1)
                modifiers.Add(new Modifier(CombatStats.Damage, ModifierKind.More, total - 1));
        }

        public bool RingPair
        {
            get
            {
                var left = _state.Gear.In(GearSlot.RingLeft);
                var right = _state.Gear.In(GearSlot.RingRight);
                return left?.Set != null && right?.Set != null && ReferenceEquals(left.Set, right.Set) && left.Set.HasSecondRing && left.SecondRing != right.SecondRing;
            }
        }

        private void AddRingPair(List<Modifier> modifiers)
        {
            var bonus = _catalog.JewelryPairBonus;
            if (!(bonus > 0) || !RingPair)
                return;
            var set = _state.Gear.In(GearSlot.RingLeft)!.Set!;
            AddScaled(modifiers, set.PieceLines(ItemSlot.Ring, false), bonus);
            AddScaled(modifiers, set.PieceLines(ItemSlot.Ring, true), bonus);
        }

        private static void AddScaled(List<Modifier> modifiers, IReadOnlyList<Modifier> lines, double scale)
        {
            foreach (var line in lines)
                modifiers.Add(line.WithValue(line.Value * scale));
        }
    }
}
