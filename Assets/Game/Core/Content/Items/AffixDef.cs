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
        public readonly ModifierCondition When;

        public readonly IReadOnlyList<TierRange>? Ranges;

        public AffixStat(StatId stat, ModifierKind kind, StatTags tags, bool ownTiers = false, double tierOne = 0, double tierLast = 0,
            ModifierCondition when = ModifierCondition.Always, IReadOnlyList<TierRange>? ranges = null)
        {
            Ranges = ranges;
            When = when;
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
            ItemRarity fromRarity = ItemRarity.Simple, Sim.SkillSlotRole? builtInRole = null, IReadOnlyList<string>? supportIds = null,
            IReadOnlyList<TierRange>? ranges = null)
        {
            Ranges = ranges;
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
        public IReadOnlyList<TierRange>? Ranges { get; }
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

        public double ValueAt(int tier, int tiers) => Ranges != null && tiers == Ranges.Count ? Ranges[tier - 1].Low : Between(TierOne, TierLast, tier, tiers);

        public double Lowest(int tier) => MainRange(tier).Low;

        public double Highest(int tier) => MainRange(tier).High;

        public TierRange MainRange(int tier) => RangeOf(TierOne, TierLast, Ranges, tier);

        public TierRange StatRange(int index, int tier) =>
            Stats[index].OwnTiers ? RangeOf(Stats[index].TierOne, Stats[index].TierLast, Stats[index].Ranges, tier) : MainRange(tier);

        public bool Spreads(int tier)
        {
            if (MainRange(tier).Spreads)
                return true;
            for (var i = 0; i < Stats.Count; i++)
            {
                if (Stats[i].OwnTiers && StatRange(i, tier).Spreads)
                    return true;
            }
            return false;
        }

        public double RollValue(int tier, double fraction) => At(TierOne, TierLast, Ranges, tier, fraction);

        public double StatValue(int index, int tier, double rolled, double roll = 0) =>
            Stats[index].OwnTiers ? At(Stats[index].TierOne, Stats[index].TierLast, Stats[index].Ranges, tier, roll) : rolled;

        public string? RangeProblem(int tiers)
        {
            var problem = TableProblem(Ranges, tiers);
            for (var i = 0; problem == null && i < Stats.Count; i++)
                problem = TableProblem(Stats[i].Ranges, tiers);
            return problem;
        }

        private static string? TableProblem(IReadOnlyList<TierRange>? ranges, int tiers)
        {
            if (ranges == null)
                return null;
            if (ranges.Count != tiers)
                return $"expected {tiers} tier ranges, got {ranges.Count}";
            var growing = ranges[ranges.Count - 1].Low >= ranges[0].Low;
            for (var i = 0; i < ranges.Count; i++)
            {
                if (growing ? ranges[i].High < ranges[i].Low : ranges[i].High > ranges[i].Low)
                    return $"tier {i + 1} range runs backwards";
                if (i > 0 && (growing ? ranges[i].Low <= ranges[i - 1].High : ranges[i].Low >= ranges[i - 1].High))
                    return $"tier {i + 1} overlaps tier {i}";
            }
            return null;
        }

        private TierRange RangeOf(double first, double last, IReadOnlyList<TierRange>? ranges, int tier) =>
            ranges != null && tier >= 1 && tier <= ranges.Count ? ranges[tier - 1] : Range(first, last, tier);

        private double At(double first, double last, IReadOnlyList<TierRange>? ranges, int tier, double fraction)
        {
            var range = RangeOf(first, last, ranges, tier);
            return Snap(range.Low + (range.High - range.Low) * fraction, ranges != null ? TableUnit(ranges) : Unit(first, last));
        }

        private TierRange Range(double first, double last, int tier)
        {
            var low = Between(first, last, tier, TierCount);
            if (TierCount <= 1)
                return new TierRange(low, low);
            var step = (last - first) / (TierCount - 1);
            var unit = Unit(first, last);
            low = Snap(low, unit);
            var high = Snap(Between(first, last, tier, TierCount) + step, unit) - System.Math.Sign(step) * unit;
            return System.Math.Abs(high - low) < unit / 2 || System.Math.Sign(high - low) != System.Math.Sign(step) ? new TierRange(low, low) : new TierRange(low, high);
        }

        private static double TableUnit(IReadOnlyList<TierRange> ranges)
        {
            var unit = 1.0;
            foreach (var range in ranges)
                unit = System.Math.Min(unit, System.Math.Min(UnitOf(range.Low), UnitOf(range.High)));
            return unit;
        }

        private static double UnitOf(double value)
        {
            if (IsWhole(value))
                return 1;
            return IsWhole(value / WholePercent) ? WholePercent : WholePercent / 10;
        }

        public string TierName(int tier) => "T" + System.Math.Max(0, TierCount - tier);

        private double Unit(double first, double last)
        {
            if (IsWhole(first) && IsWhole(last) && System.Math.Abs(last) >= 1)
                return 1;
            var step = TierCount <= 1 ? 0 : System.Math.Abs(last - first) / (TierCount - 1);
            return step >= WholePercent * 2 - 1e-9 ? WholePercent : WholePercent / 10;
        }

        private const double WholePercent = 0.01;

        private static bool IsWhole(double value) => System.Math.Abs(value - System.Math.Round(value)) < 1e-6;

        private static double Snap(double value, double unit) => System.Math.Round(System.Math.Round(value / unit) * unit, 6);

        private static double Between(double first, double last, int tier, int tiers) =>
            tiers <= 1 ? last : first + (last - first) * (tier - 1) / (tiers - 1);
    }
}
