using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class AuraBoost
    {
        private readonly StatSheet _hero;
        private readonly SkillSystem _skills;
        private readonly ModifierKind _kind;
        private Modifier _applied;
        private double _appliedBonus;

        public AuraBoost(StatSheet hero, SkillSystem skills, ModifierKind kind)
        {
            _hero = hero;
            _skills = skills;
            _kind = kind;
        }

        public void Apply(double bonus)
        {
            if (System.Math.Abs(bonus - _appliedBonus) < 1e-9)
                return;
            if (_appliedBonus > 0)
                _hero.Remove(_applied);
            _appliedBonus = bonus;
            if (bonus > 0)
            {
                _applied = new Modifier(CombatStats.AuraEffect, _kind, bonus, StatTags.Aura);
                _hero.Add(_applied);
            }
            _skills.RefreshAuraStrength();
        }
    }
}
