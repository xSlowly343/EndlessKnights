using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class CannonSystem
    {
        public const int Capacity = 16;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly ResourceSystem _resource;

        public CannonSystem(GameState state, StatSheet hero, CombatSystem combat, ResourceSystem resource)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _resource = resource;
        }

        public long Shots { get; private set; }

        public int Limit => System.Math.Min(Capacity, _combat.Cannons.Max + (int)System.Math.Max(0, System.Math.Round(_hero.Value(CombatStats.CannonCount, 0))));

        public int Standing
        {
            get
            {
                var count = 0;
                var cannons = _state.Cannons;
                for (var i = 0; i < cannons.Length; i++)
                {
                    if (cannons[i].Standing)
                        count++;
                }
                return count;
            }
        }

        public void Place(CannonEffect cannon, StatTags tags)
        {
            var index = Standing < Limit ? FreeIndex() : OldestIndex();
            if (index < 0)
                return;
            var seconds = cannon.Seconds * System.Math.Max(0, _hero.Value(CombatStats.MinionSeconds, 1, StatTags.Minion));
            _state.Cannons[index] = new CannonState
            {
                TicksLeft = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond),
                ShotsPerSecond = cannon.ShotsPerSecond,
                Multiplier = cannon.Multiplier,
                Tags = tags | StatTags.Minion
            };
        }

        public void Clear()
        {
            for (var i = 0; i < _state.Cannons.Length; i++)
                _state.Cannons[i] = default;
        }

        public void Tick(in EffectiveInput input)
        {
            Broadside(input);
            var speed = System.Math.Max(0, _hero.Value(CombatStats.MinionSpeed, 1, StatTags.Minion));
            var cannons = _state.Cannons;
            for (var i = 0; i < cannons.Length; i++)
            {
                if (!cannons[i].Standing)
                    continue;
                cannons[i].ShotCarry += cannons[i].ShotsPerSecond * speed * GameLoop.TickSeconds;
                while (cannons[i].ShotCarry >= 1 - 1e-9)
                {
                    cannons[i].ShotCarry -= 1;
                    Fire(cannons[i].Multiplier, cannons[i].Tags);
                }
                cannons[i].TicksLeft--;
            }
        }

        private void Fire(double multiplier, StatTags tags)
        {
            Shots++;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier, tags);
            _resource.Gain(_combat.Cannons.ResourcePerShot * _hero.Value(CombatStats.ResourceGain, 1));
        }

        private void Broadside(in EffectiveInput input)
        {
            if (_state.Class != HeroClass.Captain)
                return;
            var rules = _combat.Cannons;
            _state.BroadsidePx += input.MousePx;
            while (_state.BroadsidePx >= rules.BroadsidePx)
            {
                _state.BroadsidePx -= rules.BroadsidePx;
                _combat.SkillHit(_combat.BaseKeyDamage, rules.BroadsideBalls, StatTags.Skill | StatTags.Minion);
            }
        }

        private int FreeIndex()
        {
            for (var i = 0; i < _state.Cannons.Length; i++)
            {
                if (!_state.Cannons[i].Standing)
                    return i;
            }
            return -1;
        }

        private int OldestIndex()
        {
            var oldest = -1;
            for (var i = 0; i < _state.Cannons.Length; i++)
            {
                if (_state.Cannons[i].Standing && (oldest < 0 || _state.Cannons[i].TicksLeft < _state.Cannons[oldest].TicksLeft))
                    oldest = i;
            }
            return oldest;
        }
    }
}
