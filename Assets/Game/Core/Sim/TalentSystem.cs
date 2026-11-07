using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class TalentSystem
    {
        private readonly GameState _state;
        private TalentRules _rules;
        private bool _appliedChosen;
        private HeroClass _appliedClass;
        private bool _applied;

        public TalentSystem(TalentRules rules, GameState state)
        {
            _rules = rules;
            _state = state;
            Tick();
        }

        public TalentRules Rules => _rules;
        public TalentDef? Current => _state.ClassChosen ? _rules.Of(_state.Class) : null;

        public void UseRules(TalentRules rules)
        {
            _rules = rules;
            _applied = false;
            Tick();
        }

        public void Tick()
        {
            if (_applied && _appliedChosen == _state.ClassChosen && _appliedClass == _state.Class)
                return;
            _applied = true;
            _appliedChosen = _state.ClassChosen;
            _appliedClass = _state.Class;
            var perks = _state.TalentPerks;
            for (var i = 0; i < perks.Length; i++)
                perks[i] = 0;
            var talent = Current;
            if (talent == null)
                return;
            foreach (var perk in talent.Perks)
                perks[(int)perk.Perk] += perk.Value;
        }
    }
}
