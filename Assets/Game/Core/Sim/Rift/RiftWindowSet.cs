using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Content.Rift;
using Game.Core.Math;

namespace Game.Core.Sim.Rift
{
    public static class RiftWindowSet
    {
        private const ulong SetSalt = 0x5E7_0F_71EUL;

        public static SetDef? For(ItemCatalog items, RiftWindowSetRules rules, long windowId)
        {
            var candidates = new List<SetDef>();
            foreach (var set in items.Sets)
            {
                if (set.Event == null && set.Rarity == rules.Rarity && set.Pieces.Count > 0)
                    candidates.Add(set);
            }
            if (candidates.Count == 0)
                return null;
            return candidates[(int)(Mix((ulong)windowId) % (ulong)candidates.Count)];
        }

        public static IReadOnlyList<GearItem> Roll(ItemCatalog items, SetDef set, RiftWindowSetRules rules, long windowId)
        {
            var rng = new Rng((ulong)windowId ^ SetSalt);
            var pieces = new List<GearItem>();
            foreach (var piece in set.Pieces)
            {
                if (HasBase(items, piece.Slot))
                    pieces.Add(items.RollSetPiece(set, piece.Slot, null, set.Rarity, rules.ItemLevel, rng, piece.SecondRing));
            }
            return pieces;
        }

        private static bool HasBase(ItemCatalog items, ItemSlot slot)
        {
            foreach (var gearBase in items.Bases)
            {
                if (gearBase.Slot == slot)
                    return true;
            }
            return false;
        }

        private static ulong Mix(ulong value)
        {
            value ^= value >> 33;
            value *= 0xFF51AFD7ED558CCDUL;
            value ^= value >> 33;
            value *= 0xC4CEB9FE1A85EC53UL;
            return value ^ (value >> 33);
        }
    }
}
