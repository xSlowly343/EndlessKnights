using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DotSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private int _ticksToHit = GameLoop.TicksPerSecond;

        public DotSystem(GameState state, StatSheet hero, CombatSystem combat)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        private DotRules Rules => _combat.Dots;

        public double PoisonMax => Rules.PoisonMax + System.Math.Max(0, _hero.Value(CombatStats.PoisonStacks, 0, StatTags.DoT));
        public double BurnMax => Rules.BurnMax;
        public int Stir => System.Math.Min(Rules.StirMax, (int)System.Math.Floor(_state.StirTurns + 1e-9));

        public void Apply(AddStacksEffect effect)
        {
            var stacks = effect.Stacks;
            if (stacks <= 0)
                return;
            stacks += System.Math.Max(0, _hero.Value(CombatStats.DotStacks, 0, StatTags.DoT));
            if (effect.Kind == DotKind.Poison)
            {
                stacks += Stir;
                _state.StirTurns = 0;
            }
            Add(effect.Kind, stacks);
        }

        public void Flow(AddStacksEffect effect) => Add(effect.Kind, effect.PerSecond * GameLoop.TickSeconds);

        public void Detonate(DetonateEffect effect)
        {
            var hits = HitsPerSecond(1) * effect.Seconds;
            _state.PoisonStacks = 0;
            _state.BurnStacks = 0;
            Hit(hits);
        }

        public void Tick(in EffectiveInput input)
        {
            if (_state.Class == HeroClass.PlagueDoctor)
                _state.StirTurns = System.Math.Min(Rules.StirMax, _state.StirTurns + input.CircleTurns);
            var fanning = _state.Class == HeroClass.Moonshiner && input.MousePx > 0 ? Rules.FanningMultiplier : 1;
            _state.DotCarry += HitsPerSecond(fanning) * GameLoop.TickSeconds;
            if (--_ticksToHit > 0)
                return;
            _ticksToHit = GameLoop.TicksPerSecond;
            var carry = _state.DotCarry;
            _state.DotCarry = 0;
            Hit(carry);
        }

        private double HitsPerSecond(double fanning) =>
            _state.PoisonStacks * Rules.PoisonHitsPerStack + _state.BurnStacks * Rules.BurnHitsPerStack * fanning;

        private void Add(DotKind kind, double stacks)
        {
            if (kind == DotKind.Poison)
                _state.PoisonStacks = System.Math.Min(PoisonMax, _state.PoisonStacks + stacks);
            else
                _state.BurnStacks = System.Math.Min(BurnMax, _state.BurnStacks + stacks);
        }

        private void Hit(double hits)
        {
            if (hits <= 0)
                return;
            _combat.SkillHit(_combat.BaseKeyDamage, hits * System.Math.Max(0, _hero.Value(CombatStats.DotDamage, 1, StatTags.DoT)), StatTags.Skill | StatTags.DoT);
        }
    }
}
