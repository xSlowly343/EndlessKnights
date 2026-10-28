using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GearSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;

        public GearSystem(GameState state, StatSheet hero)
        {
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
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
            _layer.Set(modifiers);
        }
    }
}
