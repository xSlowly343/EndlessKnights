using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Stats
{
    public sealed class StatSheet
    {
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private readonly Dictionary<(StatId Stat, StatTags Tags), Totals> _cache = new Dictionary<(StatId, StatTags), Totals>();

        public int RecalculationCount { get; private set; }

        public void Add(Modifier modifier)
        {
            _modifiers.Add(modifier);
            Invalidate();
        }

        public void AddRange(IEnumerable<Modifier> modifiers)
        {
            _modifiers.AddRange(modifiers);
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

        private Totals TotalsFor(StatId stat, StatTags tags)
        {
            var key = (stat, tags);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var flat = 0.0;
            var increased = 0.0;
            var more = 1.0;
            foreach (var modifier in _modifiers)
            {
                if (!modifier.AppliesTo(stat, tags))
                    continue;
                switch (modifier.Kind)
                {
                    case ModifierKind.Flat:
                        flat += modifier.Value;
                        break;
                    case ModifierKind.Increased:
                        increased += modifier.Value;
                        break;
                    case ModifierKind.More:
                        more *= 1.0 + modifier.Value;
                        break;
                }
            }

            var totals = new Totals(flat, 1.0 + increased, more);
            _cache[key] = totals;
            RecalculationCount++;
            return totals;
        }

        private void Invalidate() => _cache.Clear();

        private readonly struct Totals
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
