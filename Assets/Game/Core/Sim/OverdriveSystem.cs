using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class OverdriveSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly AuraBoost _boost;

        public OverdriveSystem(GameState state, StatSheet hero, CombatSystem combat, SkillSystem skills)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _boost = new AuraBoost(hero, skills, ModifierKind.More);
        }

        public void Tick()
        {
            var rules = _combat.Classes.Overdrive;
            if (!_state.Is(HeroClass.Metalhead) || rules.Songs <= 0)
            {
                _state.OverdriveTicks = 0;
                _state.OverdriveSpent = false;
                _boost.Apply(0);
                return;
            }
            if (_state.OverdriveTicks > 0)
                _state.OverdriveTicks--;
            else if (_state.ResourceFill < 1)
                _state.OverdriveSpent = false;
            else if (!_state.OverdriveSpent)
            {
                _state.OverdriveSpent = true;
                _state.OverdriveTicks = (int)System.Math.Round((rules.Seconds + _hero.Value(CombatStats.OverdriveSeconds, 0)) * GameLoop.TicksPerSecond);
            }
            _boost.Apply(_state.OverdriveTicks > 0 ? rules.Songs + System.Math.Max(0, _hero.Value(CombatStats.OverdriveSongs, 0)) : 0);
        }
    }
}
