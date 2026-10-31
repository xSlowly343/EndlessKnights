using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ClassFeatureSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly ResourceSystem _resource;
        private readonly SkillSystem _skills;
        private readonly ModifierLayer _bondLayer;
        private readonly List<Modifier> _bondModifiers = new List<Modifier>(1);
        private double _bondShare;
        private int _ticksToBleed = GameLoop.TicksPerSecond;
        private double _bloodCarry;

        public ClassFeatureSystem(GameState state, StatSheet hero, CombatSystem combat, ResourceSystem resource, SkillSystem skills)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _resource = resource;
            _skills = skills;
            _bondLayer = new ModifierLayer(hero);
        }

        private ClassRules Rules => _combat.Classes;

        public long GolemsRaised { get; private set; }
        public long BloodTicks { get; private set; }

        public double BondShare => _state.BondsUp ? Rules.Bonds.AuraDuration * (1 + System.Math.Max(0, _hero.Value(CombatStats.BondPower, 0))) : 0;

        public double BloodHitsPerSecond => _state.Is(HeroClass.Thornmaiden) && _state.EnemyAlive
            ? _state.BloodStacks * Rules.Blood.HitsPerStack * (1 + System.Math.Max(0, _hero.Value(CombatStats.BloodPower, 0, StatTags.DoT)))
            : 0;

        public void Tick()
        {
            Bonds();
            RaiseGolem();
        }

        private void Bonds()
        {
            _state.BondsUp = _state.Is(HeroClass.Spinner) && Rules.Bonds.AuraDuration > 0 && _state.Resource > Rules.Bonds.ResourceAbove;
            var share = BondShare;
            if (share == _bondShare)
                return;
            _bondShare = share;
            _bondModifiers.Clear();
            if (share > 0)
                _bondModifiers.Add(new Modifier(CombatStats.AuraDuration, ModifierKind.Increased, share, StatTags.Aura));
            _bondLayer.Set(_bondModifiers);
        }

        private void RaiseGolem()
        {
            if (!_state.Is(HeroClass.Clayshaper) || Rules.Golem.Summon.Multiplier <= 0 || !_resource.IsFull || _resource.Max <= 0)
                return;
            if (_skills.Minions.Standing(MinionKind.Golem) > 0)
                return;
            _resource.SpendAll();
            _skills.Minions.Place(Rules.Golem.Summon, StatTags.None);
            GolemsRaised++;
        }

        public void Bleed(in EffectiveInput input)
        {
            if (!_state.Is(HeroClass.Thornmaiden))
                return;
            var rules = Rules.Blood;
            if (input.Flicks > 0 && _state.EnemyAlive)
                _state.BloodStacks = System.Math.Min(rules.Max, _state.BloodStacks + input.Flicks * rules.PerFlick);
            if (_state.BloodStacks <= 0)
                _bloodCarry = 0;
            _bloodCarry += BloodHitsPerSecond * GameLoop.TickSeconds;
            if (--_ticksToBleed > 0)
                return;
            _ticksToBleed = GameLoop.TicksPerSecond;
            var hits = _bloodCarry;
            _bloodCarry = 0;
            if (hits <= 0 || !_state.EnemyAlive)
                return;
            BloodTicks++;
            _skills.Dots.HitWith(hits);
        }
    }
}
