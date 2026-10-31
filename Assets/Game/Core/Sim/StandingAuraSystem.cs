using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class StandingAuraSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly AuraBoost _boost;

        public StandingAuraSystem(GameState state, StatSheet hero, CombatSystem combat, SkillSystem skills)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _boost = new AuraBoost(hero, skills, ModifierKind.Increased);
        }

        public void Tick()
        {
            var paused = _state.Is(HeroClass.Bannerbearer) && _state.IdleTicks * GameLoop.TickSeconds >= _combat.Classes.StandingAuras.PauseSeconds;
            _boost.Apply(paused ? System.Math.Max(0, _hero.Value(CombatStats.AuraInPause, 0)) : 0);
        }
    }
}
