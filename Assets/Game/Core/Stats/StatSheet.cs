using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Stats
{
    public sealed class StatSheet
    {
        private const int CacheCapacity = 512;
        private const int ModifierCapacity = 256;

        private readonly List<Modifier> _modifiers = new List<Modifier>(ModifierCapacity);
        private readonly Dictionary<(StatId Stat, StatTags Tags), Totals> _cache = new Dictionary<(StatId, StatTags), Totals>(CacheCapacity);

        private ulong _conditions;
        private ulong _usedConditions;

        public int RecalculationCount { get; private set; }

        public ulong Conditions => _conditions;

        public void SetConditions(ulong conditions)
        {
            var changed = (conditions ^ _conditions) & _usedConditions;
            _conditions = conditions;
            if (changed != 0)
                Invalidate();
        }

        public void Add(Modifier modifier)
        {
            _modifiers.Add(modifier);
            _usedConditions |= ModifierConditions.Bit(modifier.When);
            Invalidate();
        }

        public void AddRange(IEnumerable<Modifier> modifiers)
        {
            var first = _modifiers.Count;
            _modifiers.AddRange(modifiers);
            for (var i = first; i < _modifiers.Count; i++)
                _usedConditions |= ModifierConditions.Bit(_modifiers[i].When);
            Invalidate();
        }

        public bool Remove(Modifier modifier)
        {
            var removed = _modifiers.Remove(modifier);
            if (removed)
                Invalidate();
            return removed;
        }

        public void Clear()
        {
            _modifiers.Clear();
            _usedConditions = 0;
            Invalidate();
        }

        private const double ExactMultiplierLimit = 1e300;

        public BigNumber Compute(StatId stat, BigNumber baseValue, StatTags tags = StatTags.None)
        {
            var totals = TotalsFor(stat, tags);
            var multiplier = totals.IncreasedMultiplier * totals.MoreMultiplier;
            if (multiplier < ExactMultiplierLimit)
                return (baseValue + totals.Flat) * multiplier;
            if (!(totals.IncreasedMultiplier > 0))
                return BigNumber.Zero;
            return (baseValue + totals.Flat) * FromLog10(System.Math.Log10(totals.IncreasedMultiplier) + totals.MoreLog10);
        }

        private static BigNumber FromLog10(double log10)
        {
            var whole = System.Math.Floor(log10);
            return BigNumber.Create(System.Math.Pow(10d, log10 - whole), (long)whole);
        }

        public double Value(StatId stat, double baseValue, StatTags tags = StatTags.None)
        {
            var totals = TotalsFor(stat, tags);
            return (baseValue + totals.Flat) * totals.IncreasedMultiplier * totals.MoreMultiplier;
        }

        public double FlatWhen(StatId stat, ulong conditions)
        {
            var total = 0.0;
            foreach (var modifier in _modifiers)
            {
                if (modifier.Stat == stat && modifier.Kind == ModifierKind.Flat && (ModifierConditions.Bit(modifier.When) & conditions) != 0)
                    total += modifier.Value;
            }
            return total;
        }

        public Totals TotalsOf(StatId stat, StatTags tags = StatTags.None) => TotalsFor(stat, tags);

        private Totals TotalsFor(StatId stat, StatTags tags)
        {
            var key = (stat, tags);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var flat = 0.0;
            var increased = 0.0;
            var treeIncreased = 0.0;
            var more = 1.0;
            var moreLog10 = 0.0;
            foreach (var modifier in _modifiers)
            {
                if (!modifier.AppliesTo(stat, tags) || !modifier.ActiveUnder(_conditions))
                    continue;
                switch (modifier.Kind)
                {
                    case ModifierKind.Flat:
                        flat += modifier.Value;
                        break;
                    case ModifierKind.Increased when modifier.Pool == ModifierPool.Tree:
                        treeIncreased += modifier.Value;
                        break;
                    case ModifierKind.Increased:
                        increased += modifier.Value;
                        break;
                    case ModifierKind.More:
                        more *= 1.0 + modifier.Value;
                        moreLog10 += System.Math.Log10(System.Math.Max(0, 1.0 + modifier.Value));
                        break;
                }
            }

            var totals = new Totals(flat, (1.0 + increased) * (1.0 + treeIncreased), more, moreLog10);
            _cache[key] = totals;
            RecalculationCount++;
            return totals;
        }

        private void Invalidate() => _cache.Clear();

        public readonly struct Totals
        {
            public readonly double Flat;
            public readonly double IncreasedMultiplier;
            public readonly double MoreMultiplier;
            public readonly double MoreLog10;

            public Totals(double flat, double increasedMultiplier, double moreMultiplier, double moreLog10)
            {
                Flat = flat;
                IncreasedMultiplier = increasedMultiplier;
                MoreMultiplier = moreMultiplier;
                MoreLog10 = moreLog10;
            }
        }
    }
}
