using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class HideoutSystem
    {
        private readonly GameState _state;
        private HideoutRules _rules;

        public HideoutSystem(HideoutRules rules, GameState state)
        {
            _rules = rules;
            _state = state;
        }

        public HideoutRules Rules => _rules;
        public HideoutState State => _state.Hideout;
        public bool Inside => _state.Peaceful.Mode == PeacefulMode.Camp && _state.Peaceful.Place == PeacefulPlace.Hideout;
        public HideoutTier? NextTier => State.Tier < _rules.Tiers.Count ? _rules.Tiers[State.Tier] : null;

        public void UseRules(HideoutRules rules) => _rules = rules;

        public void Use(int tier, int members, long bosses, int orders, long sitSeconds, int fireLevel, IReadOnlyList<string> here)
        {
            var hideout = State;
            hideout.Tier = System.Math.Max(0, System.Math.Min(tier, _rules.Tiers.Count));
            hideout.Members = members;
            hideout.Bosses = bosses;
            hideout.Orders = orders;
            hideout.SitSeconds = sitSeconds;
            hideout.FireLevel = fireLevel;
            hideout.Here.Clear();
            hideout.Here.AddRange(here);
        }

        public void Leave()
        {
            Use(0, 0, 0, 0, 0, 0, System.Array.Empty<string>());
            if (_state.Peaceful.Place == PeacefulPlace.Hideout)
                _state.Peaceful.Place = PeacefulPlace.Refuge;
        }

        public List<string> Open()
        {
            var open = new List<string>();
            for (var i = 0; i < State.Tier; i++)
                open.AddRange(_rules.Tiers[i].Opens);
            return open;
        }
    }
}
