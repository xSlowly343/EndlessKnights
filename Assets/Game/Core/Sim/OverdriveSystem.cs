using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class OverdriveSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly SkillSystem _skills;
        private Modifier _applied;
        private double _appliedBonus;

        public OverdriveSystem(GameState state, StatSheet hero, CombatSystem combat, SkillSystem skills)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _skills = skills;
        }

        public void Tick()
        {
            var rules = _combat.Classes.Overdrive;
            if (!_state.Is(HeroClass.Metalhead) || rules.Songs <= 0)
            {
                _state.OverdriveTicks = 0;
                _state.OverdriveSpent = false;
                Apply(0);
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
            Apply(_state.OverdriveTicks > 0 ? rules.Songs + System.Math.Max(0, _hero.Value(CombatStats.OverdriveSongs, 0)) : 0);
        }

        private void Apply(double bonus)
        {
            if (System.Math.Abs(bonus - _appliedBonus) < 1e-9)
                return;
            if (_appliedBonus > 0)
                _hero.Remove(_applied);
            _appliedBonus = bonus;
            if (bonus > 0)
            {
                _applied = new Modifier(CombatStats.AuraEffect, ModifierKind.More, bonus, StatTags.Aura);
                _hero.Add(_applied);
            }
            _skills.RefreshAuraStrength();
        }
    }
}
