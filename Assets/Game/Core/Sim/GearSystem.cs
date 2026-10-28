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
            foreach (var slot in set.Slots)
            {
                if (!_state.SetCollection.Contains(SetDef.PieceKey(set.Id, slot)))
                    return false;
            }
            return true;
        }

        public int CollectedPieces(SetDef set)
        {
            var count = 0;
            foreach (var slot in set.Slots)
            {
                if (_state.SetCollection.Contains(SetDef.PieceKey(set.Id, slot)))
                    count++;
            }
            return count;
        }

        public bool NoteFound(GearItem item)
        {
            if (item.Set == null || !_state.SetCollection.Add(SetDef.PieceKey(item.Set.Id, item.Slot)))
                return false;
            if (Collected(item.Set))
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
            foreach (var set in _catalog.Sets)
            {
                if (Collected(set))
                    modifiers.AddRange(set.Bonus);
            }
            _layer.Set(modifiers);
            Rebuilt?.Invoke();
        }
    }
}
