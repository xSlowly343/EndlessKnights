using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class StrikeUpgradeSystem
    {
        private const double ExactPowerLimit = 300;
        private const double MinCostShare = 0.1;
        private const double ChunkLog10 = 300;
        private const int MaxChunks = 16;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private readonly Modifier[][] _chunks = Chunks();
        private StrikeUpgradeRules _rules;

        public StrikeUpgradeSystem(StrikeUpgradeRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public int Level => _state.StrikeLevel;

        public double DamageMultiplier => _rules.DamageMultiplier(Level);

        public double DamageMultiplierAt(int level) => _rules.DamageMultiplier(level);

        public void UseRules(StrikeUpgradeRules rules)
        {
            _rules = rules;
            Rebuild();
        }

        public BigNumber CostFor(int levels)
        {
            if (levels <= 0)
                return BigNumber.Zero;
            var rate = _rules.Cost.Rate;
            var next = NextCost;
            if (rate <= 1)
                return next * levels;
            var exponent = levels * System.Math.Log10(rate);
            var sumLog10 = exponent < ExactPowerLimit
                ? System.Math.Log10((System.Math.Pow(rate, levels) - 1) / (rate - 1))
                : exponent - System.Math.Log10(rate - 1);
            return next * ExponentialCurve.FromLog10(sumLog10);
        }

        private BigNumber NextCost => _rules.Cost.At(Level + 1) * System.Math.Max(MinCostShare, _hero.Value(CombatStats.UpgradeCost, 1));

        public int Affordable()
        {
            var next = NextCost;
            if (_state.Gold < next)
                return 0;
            var rate = _rules.Cost.Rate;
            var ratioLog10 = (_state.Gold / next).Log10();
            int levels;
            if (rate <= 1)
            {
                levels = (int)System.Math.Min(int.MaxValue / 2, System.Math.Pow(10, ratioLog10));
            }
            else
            {
                var scaled = ratioLog10 + System.Math.Log10(rate - 1);
                var sumLog10 = scaled > 15 ? scaled : System.Math.Log10(1 + System.Math.Pow(10, scaled));
                levels = (int)System.Math.Min(int.MaxValue / 2, System.Math.Floor(sumLog10 / System.Math.Log10(rate)));
            }
            while (levels > 0 && CostFor(levels) > _state.Gold)
                levels--;
            while (CostFor(levels + 1) <= _state.Gold)
                levels++;
            return levels;
        }

        public bool TryBuy(int levels)
        {
            if (levels <= 0)
                return false;
            var cost = CostFor(levels);
            if (_state.Gold < cost)
                return false;
            _state.Gold -= cost;
            _state.StrikeLevel += levels;
            Rebuild();
            return true;
        }

        public int BuyMax()
        {
            var levels = Affordable();
            return TryBuy(levels) ? levels : 0;
        }

        public void Reset()
        {
            _state.StrikeLevel = 0;
            Rebuild();
        }

        public double DamageMultiplierLog10 => _rules.DamageMultiplierLog10(Level);

        public void Rebuild()
        {
            var log10 = DamageMultiplierLog10;
            var count = System.Math.Min(MaxChunks, System.Math.Max(1, (int)System.Math.Ceiling(log10 / ChunkLog10)));
            var chunk = _chunks[count - 1];
            var more = count == 1 ? DamageMultiplier - 1 : System.Math.Pow(10, log10 / count) - 1;
            for (var i = 0; i < count; i++)
                chunk[i] = new Modifier(CombatStats.Damage, ModifierKind.More, more);
            _layer.Set(chunk);
        }

        private static Modifier[][] Chunks()
        {
            var chunks = new Modifier[MaxChunks][];
            for (var i = 0; i < MaxChunks; i++)
                chunks[i] = new Modifier[i + 1];
            return chunks;
        }
    }
}
