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

        private static readonly int PoolCount = System.Enum.GetValues(typeof(ModifierPool)).Length;

        private readonly double[] _poolIncreased = new double[PoolCount];
        private readonly double[] _poolExpected = new double[PoolCount];

        private ulong _conditions;
        private ulong _usedConditions;

        public int RecalculationCount { get; private set; }

        public int ModifierVersion { get; private set; }

        public IReadOnlyList<Modifier> Modifiers => _modifiers;

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
            ModifierVersion++;
            Invalidate();
        }

        public void AddRange(IEnumerable<Modifier> modifiers)
        {
            var first = _modifiers.Count;
            _modifiers.AddRange(modifiers);
            for (var i = first; i < _modifiers.Count; i++)
                _usedConditions |= ModifierConditions.Bit(_modifiers[i].When);
            ModifierVersion++;
            Invalidate();
        }

        public bool Remove(Modifier modifier)
        {
            var removed = _modifiers.Remove(modifier);
            if (removed)
            {
                ModifierVersion++;
                Invalidate();
            }
            return removed;
        }

        public void Clear()
        {
            _modifiers.Clear();
            _usedConditions = 0;
            ModifierVersion++;
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
            ClearPools();
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
                    case ModifierKind.Increased:
                        _poolIncreased[(int)modifier.Pool] += modifier.Value;
                        break;
                    case ModifierKind.More:
                        more *= 1.0 + modifier.Value;
                        moreLog10 += System.Math.Log10(System.Math.Max(0, 1.0 + modifier.Value));
                        break;
                }
            }

            var totals = new Totals(flat, PoolProduct(_poolIncreased), more, moreLog10);
            _cache[key] = totals;
            RecalculationCount++;
            return totals;
        }

        public double ConditionalFactor(StatId stat, double baseValue, StatTags tags, IReadOnlyList<double> conditionShares)
        {
            var flat = 0.0;
            var flatExpected = 0.0;
            ClearPools();
            var more = 1.0;
            foreach (var modifier in _modifiers)
            {
                if (!modifier.AppliesTo(stat, tags))
                    continue;
                var always = modifier.When == ModifierCondition.Always;
                var weight = always ? 1 : ShareOf(conditionShares, modifier.When);
                var own = always ? modifier.Value : 0;
                switch (modifier.Kind)
                {
                    case ModifierKind.Flat:
                        flat += own;
                        flatExpected += modifier.Value * weight;
                        break;
                    case ModifierKind.Increased:
                        _poolIncreased[(int)modifier.Pool] += own;
                        _poolExpected[(int)modifier.Pool] += modifier.Value * weight;
                        break;
                    case ModifierKind.More when !always:
                        more *= 1.0 + modifier.Value * weight;
                        break;
                }
            }
            return Ratio(baseValue + flatExpected, baseValue + flat) * PoolRatio() * more;
        }

        public double ExpectedValue(StatId stat, double baseValue, StatTags tags, IReadOnlyList<double> conditionShares)
        {
            var flat = 0.0;
            ClearPools();
            var more = 1.0;
            foreach (var modifier in _modifiers)
            {
                if (!modifier.AppliesTo(stat, tags))
                    continue;
                var weight = modifier.When == ModifierCondition.Always ? 1 : ShareOf(conditionShares, modifier.When);
                switch (modifier.Kind)
                {
                    case ModifierKind.Flat:
                        flat += modifier.Value * weight;
                        break;
                    case ModifierKind.Increased:
                        _poolIncreased[(int)modifier.Pool] += modifier.Value * weight;
                        break;
                    case ModifierKind.More:
                        more *= 1.0 + modifier.Value * weight;
                        break;
                }
            }
            return (baseValue + flat) * PoolProduct(_poolIncreased) * more;
        }

        private void ClearPools()
        {
            System.Array.Clear(_poolIncreased, 0, PoolCount);
            System.Array.Clear(_poolExpected, 0, PoolCount);
        }

        private static double PoolProduct(double[] pools)
        {
            var product = 1.0;
            foreach (var increased in pools)
                product *= 1.0 + increased;
            return product;
        }

        private double PoolRatio()
        {
            var ratio = 1.0;
            for (var i = 0; i < PoolCount; i++)
                ratio *= Ratio(1 + _poolExpected[i], 1 + _poolIncreased[i]);
            return ratio;
        }

        private static double Ratio(double expected, double plain) => plain > 0 ? System.Math.Max(0, expected) / plain : 1;

        private static double ShareOf(IReadOnlyList<double> shares, ModifierCondition condition)
        {
            var index = (int)condition;
            return index < shares.Count ? System.Math.Min(1, System.Math.Max(0, shares[index])) : 0;
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
