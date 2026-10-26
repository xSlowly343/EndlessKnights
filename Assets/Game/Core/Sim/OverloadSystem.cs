using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class OverloadSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ResourceSystem _resource;
        private readonly CombatSystem _combat;

        public OverloadSystem(GameState state, StatSheet hero, ResourceSystem resource, CombatSystem combat)
        {
            _state = state;
            _hero = hero;
            _resource = resource;
            _combat = combat;
        }

        public long Discharges { get; private set; }

        public void Tick()
        {
            var multiplier = _resource.Rules.OverloadMultiplier;
            if (multiplier <= 0 || !_resource.IsFull || _resource.Max <= 0)
                return;
            if (_hero.Value(CombatStats.OverloadDisabled, 0) > 0)
                return;
            _resource.SpendAll();
            Discharges++;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier + _hero.Value(CombatStats.OverloadMultiplier, 0, StatTags.Skill), StatTags.Skill);
        }
    }
}
