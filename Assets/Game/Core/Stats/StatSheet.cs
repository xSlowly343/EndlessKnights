using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Stats
{
    public sealed class StatSheet
    {
        private const int CacheCapacity = 512;

        private readonly List<Modifier> _modifiers = new List<Modifier>();
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

        public BigNumber Compute(StatId stat, BigNumber baseValue, StatTags tags = StatTags.None)
        {
            var totals = TotalsFor(stat, tags);
            return (baseValue + totals.Flat) * (totals.IncreasedMultiplier * totals.MoreMultiplier);
        }

        public double Value(StatId stat, double baseValue, StatTags tags = StatTags.None)
        {
            var totals = TotalsFor(stat, tags);
            return (baseValue + totals.Flat) * totals.IncreasedMultiplier * totals.MoreMultiplier;
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
                        break;
                }
            }

            var totals = new Totals(flat, (1.0 + increased) * (1.0 + treeIncreased), more);
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

            public Totals(double flat, double increasedMultiplier, double moreMultiplier)
            {
                Flat = flat;
                IncreasedMultiplier = increasedMultiplier;
                MoreMultiplier = moreMultiplier;
            }
        }
    }
}
