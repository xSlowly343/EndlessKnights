using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class PathSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;

        public PathSystem(GameState state, StatSheet hero, CombatSystem combat)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public long IdleHits { get; private set; }
        public long CompanionHits { get; private set; }

        public void Tick(in EffectiveInput input)
        {
            IdleStrikes(input);
            Companion();
        }

        private void IdleStrikes(in EffectiveInput input)
        {
            var perSecond = _hero.Value(CombatStats.IdleHitsPerSecond, 0, StatTags.Idle);
            if (perSecond <= 0 || input.HasInput)
            {
                _state.IdleHitCarry = 0;
                return;
            }
            var hits = TakeHits(_state.IdleHitCarry + perSecond * GameLoop.TickSeconds, out var carry);
            _state.IdleHitCarry = carry;
            for (var i = 0; i < hits; i++)
            {
                _combat.SkillHit(_combat.BaseKeyDamage, 1, StatTags.Idle);
                IdleHits++;
            }
        }

        private void Companion()
        {
            var perSecond = _hero.Value(CombatStats.CompanionHitsPerSecond, 0, StatTags.Minion);
            if (perSecond <= 0)
            {
                _state.CompanionHitCarry = 0;
                return;
            }
            var speed = System.Math.Max(0, _hero.Value(CombatStats.MinionSpeed, 1, StatTags.Minion));
            var hits = TakeHits(_state.CompanionHitCarry + perSecond * speed * GameLoop.TickSeconds, out var carry);
            _state.CompanionHitCarry = carry;
            var multiplier = System.Math.Max(0, _hero.Value(CombatStats.CompanionMultiplier, 1, StatTags.Minion));
            for (var i = 0; i < hits; i++)
            {
                _combat.SkillHit(_combat.BaseKeyDamage, multiplier, StatTags.Skill | StatTags.Minion);
                CompanionHits++;
            }
        }

        private static int TakeHits(double amount, out double carry)
        {
            var whole = System.Math.Floor(amount + 1e-9);
            carry = System.Math.Max(0, amount - whole);
            return (int)whole;
        }
    }
}
