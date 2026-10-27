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

        public const double MaxKeep = 0.9;

        public long Discharges { get; private set; }
        public long Bursts { get; private set; }

        public void Tick()
        {
            if (_resource.Rules.Inverse)
            {
                Burst();
                return;
            }
            var multiplier = _resource.Rules.OverloadMultiplier;
            if (multiplier <= 0)
                multiplier = _hero.Value(CombatStats.OverloadAtMax, 0, StatTags.Skill);
            if (multiplier <= 0 || !_resource.IsFull || _resource.Max <= 0)
                return;
            if (_hero.Value(CombatStats.OverloadDisabled, 0) > 0)
                return;
            var keep = System.Math.Min(MaxKeep, System.Math.Max(0, _hero.Value(CombatStats.OverloadKeep, 0)));
            if (keep > 0)
                _resource.TrySpend(_state.Resource - keep * _resource.Max);
            else
                _resource.SpendAll();
            Discharges++;
            var power = multiplier + _hero.Value(CombatStats.OverloadMultiplier, 0, StatTags.Skill);
            _combat.SkillHit(_combat.BaseKeyDamage, power, StatTags.Skill);
            var share = _hero.Value(CombatStats.OverloadQueueShare, 0, StatTags.Skill);
            if (share > 0)
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, power * share, StatTags.Skill));
        }

        private void Burst()
        {
            var power = _hero.Value(CombatStats.OverheatBurst, 0, StatTags.Skill);
            if (power <= 0 || _resource.Max <= 0 || _state.Resource < _resource.Max - 1e-9)
                return;
            _resource.SpendAll();
            Bursts++;
            _combat.SkillHit(_combat.BaseKeyDamage, power, StatTags.Skill);
        }
    }
}
