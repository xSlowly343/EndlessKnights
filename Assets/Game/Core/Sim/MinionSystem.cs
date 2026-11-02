using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class MinionSystem
    {
        public const int Capacity = 32;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly DotSystem _dots;

        public MinionSystem(GameState state, StatSheet hero, CombatSystem combat, DotSystem dots)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _dots = dots;
        }

        public long SkeletonHits { get; private set; }
        public long BeastHits { get; private set; }

        public int Limit(MinionKind kind)
        {
            var limit = Rules(kind).Max + _hero.Value(CombatStats.MinionCount, 0, StatTags.Minion)
                + (kind == MinionKind.Beast ? _hero.Value(CombatStats.BeastCount, 0, StatTags.Minion) : 0);
            return System.Math.Min(Capacity, (int)System.Math.Max(0, System.Math.Round(limit)));
        }

        public int Standing(MinionKind kind)
        {
            var count = 0;
            var minions = _state.Minions;
            for (var i = 0; i < minions.Length; i++)
            {
                if (minions[i].Standing && minions[i].Kind == kind)
                    count++;
            }
            return count;
        }

        public void Place(SummonEffect summon, StatTags tags)
        {
            var index = Standing(summon.Kind) < Limit(summon.Kind) ? FreeIndex() : OldestIndex(summon.Kind);
            if (index < 0)
                return;
            SupportEffects.MinionBonuses(_combat.Supporting, out var mouseBonus, out var life);
            var seconds = summon.Seconds * System.Math.Max(0, _hero.Value(CombatStats.MinionSeconds, 1, StatTags.Minion)) * life;
            _state.Minions[index] = new MinionState
            {
                Kind = summon.Kind,
                TicksLeft = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond),
                HitsPerSecond = summon.HitsPerSecond,
                Multiplier = summon.Multiplier,
                Tags = tags | StatTags.Minion,
                MouseBonus = mouseBonus
            };
        }

        public bool Consume(MinionKind kind, bool respawn)
        {
            var index = OldestIndex(kind);
            if (index < 0)
                return false;
            if (respawn)
                _state.Minions[index].TicksLeft = (int)System.Math.Round(Fresh(kind).Seconds * System.Math.Max(0, _hero.Value(CombatStats.MinionSeconds, 1, StatTags.Minion)) * GameLoop.TicksPerSecond);
            else
            {
                Explode(_state.Minions[index]);
                _state.Minions[index] = default;
            }
            return true;
        }

        private void Explode(in MinionState minion)
        {
            if (minion.Hits <= 0)
                return;
            var share = _hero.Value(CombatStats.MinionExplode, 0, StatTags.Minion);
            if (share > 0)
                _combat.SkillHit(_combat.BaseKeyDamage, minion.Multiplier * minion.Hits * share, minion.Tags);
        }

        public void Clear()
        {
            for (var i = 0; i < _state.Minions.Length; i++)
                _state.Minions[i] = default;
        }

        public void Tick(in EffectiveInput input)
        {
            var speed = System.Math.Max(0, _hero.Value(CombatStats.MinionSpeed, 1, StatTags.Minion));
            var minions = _state.Minions;
            for (var i = 0; i < minions.Length; i++)
            {
                if (!minions[i].Standing)
                    continue;
                var kindSpeed = input.MousePx > 0 ? speed * Rules(minions[i].Kind).MouseSpeed : speed;
                if (input.MousePx > 0 && minions[i].MouseBonus > 0)
                    kindSpeed *= minions[i].MouseBonus;
                minions[i].HitCarry += minions[i].HitsPerSecond * kindSpeed * GameLoop.TickSeconds;
                while (minions[i].HitCarry >= 1 - 1e-9)
                {
                    minions[i].HitCarry -= 1;
                    minions[i].Hits++;
                    Hit(minions[i].Kind, minions[i].Multiplier, minions[i].Tags);
                }
                minions[i].TicksLeft--;
                if (!minions[i].Standing)
                    Explode(minions[i]);
            }
        }

        private void Hit(MinionKind kind, double multiplier, StatTags tags)
        {
            if (_state.FightingBoss)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.MinionBoss, 0, StatTags.Minion));
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier, tags);
            var share = _hero.Value(CombatStats.MinionQueueShare, 0, StatTags.Minion);
            if (share > 0)
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * share, tags));
            Ignite();
            if (kind == MinionKind.Skeleton)
                SkeletonHits++;
            if (kind == MinionKind.Beast)
                BeastHits++;
        }

        private void Ignite()
        {
            var stacks = _hero.Value(CombatStats.SummonsIgnite, 0, StatTags.Minion);
            if (stacks > 0 && _state.EnemyAlive)
                _dots.AddBurn(stacks);
        }

        private SummonEffect Fresh(MinionKind kind) => kind == MinionKind.Beast ? _combat.Paths.BeastOnKill : _combat.Paths.SkeletonOnKill;

        private MinionRules Rules(MinionKind kind) => kind == MinionKind.Beast ? _combat.Beasts : _combat.Skeletons;

        private int FreeIndex()
        {
            for (var i = 0; i < _state.Minions.Length; i++)
            {
                if (!_state.Minions[i].Standing)
                    return i;
            }
            return -1;
        }

        private int OldestIndex(MinionKind kind)
        {
            var oldest = -1;
            var minions = _state.Minions;
            for (var i = 0; i < minions.Length; i++)
            {
                if (minions[i].Standing && minions[i].Kind == kind && (oldest < 0 || minions[i].TicksLeft < minions[oldest].TicksLeft))
                    oldest = i;
            }
            return oldest;
        }
    }
}
