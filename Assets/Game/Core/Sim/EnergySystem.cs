using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class EnergySystem
    {
        private readonly GameState _state;
        private EnergyRules _rules;

        public EnergySystem(EnergyRules rules, GameState state)
        {
            _rules = rules;
            _state = state;
        }

        public void UseRules(EnergyRules rules) => _rules = rules;

        public double Max => _rules.Max;
        public bool IsFull => _state.Energy >= _rules.Max;

        public void Tick(in EffectiveInput input)
        {
            var gained = input.Keys * _rules.PerKey + input.Clicks * _rules.PerClick + input.MousePx / _rules.MousePxPerPoint;
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
