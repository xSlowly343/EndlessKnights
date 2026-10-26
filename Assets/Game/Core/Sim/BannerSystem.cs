using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class BannerSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly SkillSystem _skills;
        private Modifier _applied;
        private double _appliedBonus;

        public BannerSystem(GameState state, StatSheet hero, CombatSystem combat, SkillSystem skills)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _skills = skills;
        }

        private BannerRules Rules => _combat.Banner;

        public void Tick(in EffectiveInput input)
        {
            if (_state.Class != HeroClass.Bannerbearer)
            {
                Apply(0);
                return;
            }
            var rules = Rules;
            _state.BannerPx += input.MousePx;
            while (_state.BannerPx >= rules.MousePx)
            {
                _state.BannerPx -= rules.MousePx;
                _state.BannerBonus = System.Math.Min(rules.Max, _state.BannerBonus + rules.Step);
                _state.BannerDecayTicks = 0;
            }
            if (_state.BannerBonus > 0 && ++_state.BannerDecayTicks >= rules.DecayEverySeconds * GameLoop.TicksPerSecond)
            {
                _state.BannerDecayTicks = 0;
                _state.BannerBonus = System.Math.Max(0, _state.BannerBonus - rules.Decay);
            }
            Apply(_state.BannerBonus);
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
                _applied = new Modifier(CombatStats.AuraEffect, ModifierKind.Increased, bonus, StatTags.Aura);
                _hero.Add(_applied);
            }
            _skills.RefreshAuraStrength();
        }
    }
}
