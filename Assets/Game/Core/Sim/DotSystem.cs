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

        public double PoisonHits(double stacks) => stacks * Rules.PoisonHitsPerStack * Rules.MiasmaStackSeconds;

        public void HitWith(double hits) => Hit(hits);

        public double PoisonMax => (Rules.PoisonMax + System.Math.Max(0, _hero.Value(CombatStats.PoisonStacks, 0, StatTags.DoT))) * _state.SupportStackCap;
        public double BurnMax => Rules.BurnMax * _state.SupportStackCap;
        public long PoisonTicks { get; private set; }


        public void AddBurn(double stacks) => Add(DotKind.Burn, stacks);

        public long Bursts { get; private set; }

        private void Detonate(double seconds)
        {
            var hits = HitsPerSecond() * seconds;
            _state.PoisonStacks = 0;
            _state.BurnStacks = 0;
            Hit(hits);
        }

        public void Tick(in EffectiveInput input)
        {
            if (_state.Is(HeroClass.Alchemist))
                _state.StirTurns = System.Math.Min(Rules.StirMax, _state.StirTurns + input.MousePx / Rules.StirPxPerTurn);
            StacksFromKeys(input);
            BurstAtMax();
            _state.DotCarry += HitsPerSecond() * GameLoop.TickSeconds;
            if (--_ticksToHit > 0)
                return;
            _ticksToHit = GameLoop.TicksPerSecond;
            if (_state.PoisonStacks > 0)
                PoisonTicks++;
            var carry = _state.DotCarry;
            _state.DotCarry = 0;
            Hit(carry);
        }

        private void StacksFromKeys(in EffectiveInput input)
        {
            if (input.Hits <= 0)
                return;
            if (_state.Is(HeroClass.Alchemist) && Rules.VapourKeyHits > 0)
                Add(DotKind.Poison, input.Hits / Rules.VapourKeyHits);
            var poison = _hero.Value(CombatStats.KeyHitPoison, 0, StatTags.DoT);
            if (poison > 0)
                Add(DotKind.Poison, poison * input.Hits);
            var burn = _hero.Value(CombatStats.KeyHitBurn, 0, StatTags.DoT);
            if (burn > 0)
                Add(DotKind.Burn, burn * input.Hits);
        }

        private void BurstAtMax()
        {
            var seconds = _hero.Value(CombatStats.DetonateAtMax, 0, StatTags.DoT);
            if (seconds <= 0)
                return;
            var poisonFull = PoisonMax > 0 && _state.PoisonStacks >= PoisonMax - 1e-9;
            var burnFull = BurnMax > 0 && _state.BurnStacks >= BurnMax - 1e-9;
            if (!poisonFull && !burnFull)
                return;
            Bursts++;
            Detonate(seconds);
        }

        private double HitsPerSecond() =>
            _state.PoisonStacks * Rules.PoisonHitsPerStack
            + _state.BurnStacks * Rules.BurnHitsPerStack * System.Math.Max(0, _hero.Value(CombatStats.BurnDamage, 1, StatTags.DoT));

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
            var multiplier = hits * System.Math.Max(0, _hero.Value(CombatStats.DotDamage, 1, StatTags.DoT));
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier, StatTags.Skill | StatTags.DoT);
            var share = _hero.Value(CombatStats.DotQueueShare, 0, StatTags.DoT);
            if (share > 0)
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * share, StatTags.Skill | StatTags.DoT));
        }
    }
}
