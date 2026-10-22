using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class EnergySystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private EnergyRules _rules;

        public EnergySystem(EnergyRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
        }

        public void UseRules(EnergyRules rules) => _rules = rules;

        public double Max => _rules.Max;
        public bool IsFull => _state.Energy >= _rules.Max;

        public void Tick(in EffectiveInput input)
        {
            var gained = input.Keys * _rules.PerKey * _hero.Value(CombatStats.EnergyGain, 1, StatTags.Keyboard)
                + input.Clicks * _rules.PerClick * _hero.Value(CombatStats.EnergyGain, 1, StatTags.Click)
                + input.MousePx / _rules.MousePxPerPoint * _hero.Value(CombatStats.EnergyGain, 1, StatTags.Mouse);
            _state.Energy = System.Math.Min(_rules.Max, _state.Energy + gained);
        }

        public bool TrySpend(double amount)
        {
            if (amount < 0 || _state.Energy < amount)
                return false;
            _state.Energy -= amount;
            return true;
        }
    }
}
