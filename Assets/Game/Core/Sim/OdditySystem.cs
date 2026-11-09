using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class OdditySystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly EventQueue _events;
        private readonly StageSystem _stage;
        private readonly LootSystem _loot;
        private readonly DropSystem _drops;
        private Modifier _sprout = new Modifier(CombatStats.Damage, ModifierKind.Increased, 0);
        private Modifier _spree = new Modifier(CombatStats.Damage, ModifierKind.More, 0);

        public OdditySystem(GameState state, StatSheet hero, EventQueue events, StageSystem stage, LootSystem loot, DropSystem drops)
        {
            _state = state;
            _hero = hero;
            _events = events;
            _stage = stage;
            _loot = loot;
            _drops = drops;
            _hero.Add(_sprout);
            _hero.Add(_spree);
        }

        private OddityRules Rules => _stage.Encounters.Oddities;

        public void Tick(in EffectiveInput input)
        {
            TickSprees();
            SyncSprout();
            SyncSpree();
            TickWisp(input);
        }

        private void TickSprees()
        {
            var oddities = _state.Oddities;
            if (oddities.GoldSpreeTicks > 0)
                oddities.GoldSpreeTicks--;
            if (oddities.DamageSpreeTicks > 0)
                oddities.DamageSpreeTicks--;
        }

        private void SyncSprout()
        {
            var value = _state.Oddities.SproutStacks * Rules.SproutDamage;
            if (_sprout.Value.Equals(value))
                return;
            _hero.Remove(_sprout);
            _sprout = _sprout.WithValue(value);
            _hero.Add(_sprout);
        }

        private void SyncSpree()
        {
            var oddities = _state.Oddities;
            var value = oddities.DamageSpreeTicks > 0 ? oddities.DamageSpreeMultiplier - 1 : 0;
            if (_spree.Value.Equals(value))
                return;
            _hero.Remove(_spree);
            _spree = _spree.WithValue(value);
            _hero.Add(_spree);
        }

        private void TickWisp(in EffectiveInput input)
        {
            var rules = Rules;
            if (rules.WispRewards.Length == 0)
                return;
            var oddities = _state.Oddities;
            if (oddities.WispFlying)
            {
                oddities.WispActions += input.Hits + input.ButtonPresses + input.MousePx / rules.WispMousePxPerAction;
                if (oddities.WispActions >= rules.WispActions)
                {
                    Catch(rules);
                    return;
                }
                if (--oddities.WispTicks > 0)
                    return;
                oddities.WispActions = 0;
                oddities.WispCooldownTicks = Ticks(rules.WispEverySeconds);
                _events.Add(GameEvent.ForStage(GameEventKind.WispEscaped, _state.Tick, _state.Stage));
                return;
            }
            if (oddities.WispCooldownTicks < 0)
                oddities.WispCooldownTicks = Ticks(rules.WispEverySeconds);
            if (--oddities.WispCooldownTicks > 0)
                return;
            oddities.WispTicks = Ticks(rules.WispFlySeconds);
            oddities.WispActions = 0;
            _events.Add(GameEvent.ForStage(GameEventKind.WispAppeared, _state.Tick, _state.Stage));
        }

        private void Catch(OddityRules rules)
        {
            var oddities = _state.Oddities;
            oddities.WispTicks = 0;
            oddities.WispActions = 0;
            oddities.WispCooldownTicks = Ticks(rules.WispEverySeconds);
            var reward = Pick(rules);
            Give(reward);
            _events.Add(GameEvent.WithAmount(GameEventKind.WispCaught, _state.Tick, BigNumber.FromDouble((int)reward.Kind), _state.Stage));
        }

        private WispReward Pick(OddityRules rules)
        {
            var roll = _state.Oddities.Rng.NextDouble() * rules.WispRewardWeight;
            var rewards = rules.WispRewards;
            for (var i = 0; i < rewards.Length; i++)
            {
                if (roll < rewards[i].Weight)
                    return rewards[i];
                roll -= rewards[i].Weight;
            }
            return rewards[rewards.Length - 1];
        }

        private void Give(WispReward reward)
        {
            var oddities = _state.Oddities;
            switch (reward.Kind)
            {
                case WispRewardKind.Gold:
                    _loot.GiveKills(_state.Stage, reward.Amount);
                    break;
                case WispRewardKind.Resource:
                    oddities.PendingResourceShare += reward.Amount;
                    break;
                case WispRewardKind.GoldSpree:
                    oddities.GoldSpreeMultiplier = reward.Amount;
                    oddities.GoldSpreeTicks = Ticks(reward.Seconds);
                    break;
                case WispRewardKind.DamageSpree:
                    oddities.DamageSpreeMultiplier = reward.Amount;
                    oddities.DamageSpreeTicks = Ticks(reward.Seconds);
                    break;
                case WispRewardKind.Shards:
                    _drops.GiveShards((long)System.Math.Round(reward.Amount));
                    break;
            }
        }

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
