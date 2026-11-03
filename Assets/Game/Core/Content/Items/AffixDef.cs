using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public readonly struct AffixStat
    {
        public readonly StatId Stat;
        public readonly ModifierKind Kind;
        public readonly StatTags Tags;
        public readonly bool OwnTiers;
        public readonly double TierOne;
        public readonly double TierLast;

        public AffixStat(StatId stat, ModifierKind kind, StatTags tags, bool ownTiers = false, double tierOne = 0, double tierLast = 0)
        {
            Stat = stat;
            Kind = kind;
            Tags = tags;
            OwnTiers = ownTiers;
            TierOne = tierOne;
            TierLast = tierLast;
        }
    }

    public sealed class AffixDef
    {
        public AffixDef(string id, IReadOnlyList<ItemSlot> slots, bool prefix, IReadOnlyList<AffixStat> stats, double tierOne, double tierLast, string? group,
            ItemRarity fromRarity = ItemRarity.Simple, Sim.SkillSlotRole? builtInRole = null, IReadOnlyList<string>? supportIds = null)
        {
            FromRarity = fromRarity;
            BuiltInRole = builtInRole;
            SupportIds = supportIds ?? System.Array.Empty<string>();
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
        public ItemRarity FromRarity { get; }
        public Sim.SkillSlotRole? BuiltInRole { get; }
        public IReadOnlyList<string> SupportIds { get; }
        public IReadOnlyList<Gems.GemDef> Supports { get; private set; } = System.Array.Empty<Gems.GemDef>();

        public void BindSupports(IReadOnlyList<Gems.GemDef> supports) => Supports = supports;

        public bool FitsSlot(ItemSlot slot)
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == slot)
                    return true;
            }
            return false;
        }

        public int TierCount { get; private set; } = 1;

        public void UseTierCount(int tiers) => TierCount = tiers;

        public double ValueAt(int tier, int tiers) => Between(TierOne, TierLast, tier, tiers);

        public double StatValue(int index, int tier, double rolled) =>
            Stats[index].OwnTiers ? Between(Stats[index].TierOne, Stats[index].TierLast, tier, TierCount) : rolled;

        private static double Between(double first, double last, int tier, int tiers) =>
            tiers <= 1 ? last : first + (last - first) * (tier - 1) / (tiers - 1);
    }
}
