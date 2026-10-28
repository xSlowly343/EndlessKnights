using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public readonly struct AffixStat
    {
        public readonly StatId Stat;
        public readonly ModifierKind Kind;
        public readonly StatTags Tags;

        public AffixStat(StatId stat, ModifierKind kind, StatTags tags)
        {
            Stat = stat;
            Kind = kind;
            Tags = tags;
        }
    }

    public sealed class AffixDef
    {
        public AffixDef(string id, IReadOnlyList<ItemSlot> slots, bool prefix, IReadOnlyList<AffixStat> stats, double tierOne, double tierLast, string? group)
        {
            Id = id;
            Slots = slots;
            Prefix = prefix;
            Stats = stats;
            TierOne = tierOne;
            TierLast = tierLast;
            Group = group;
        }

        public string Id { get; }
        public IReadOnlyList<ItemSlot> Slots { get; }
        public bool Prefix { get; }
        public IReadOnlyList<AffixStat> Stats { get; }
        public double TierOne { get; }
        public double TierLast { get; }
        public string? Group { get; }

        public bool FitsSlot(ItemSlot slot)
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == slot)
                    return true;
            }
            return false;
        }

        public double ValueAt(int tier, int tiers) =>
            tiers <= 1 ? TierLast : TierOne + (TierLast - TierOne) * (tier - 1) / (tiers - 1);
    }
}
