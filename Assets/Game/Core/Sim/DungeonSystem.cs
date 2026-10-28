using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim.Rift;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DungeonSystem
    {
        private readonly GameState _state;
        private readonly StageSystem _stage;
        private readonly DropSystem _drops;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private DungeonRules _rules;
        private StageTable _home;
        private EnemyCatalog _enemies;

        public DungeonSystem(GameData data, GameState state, StatSheet hero, StageSystem stage, DropSystem drops, EventQueue events)
        {
            _rules = data.Dungeons;
            _home = data.Stages;
            _enemies = data.Enemies;
            _state = state;
            _stage = stage;
            _drops = drops;
            _events = events;
            _layer = new ModifierLayer(hero);
            if (Inside)
                ApplyWorld();
        }

        public Unlocks? Unlocks { get; set; }
        public DungeonRules Rules => _rules;
        public bool Inside => _state.Dungeons.Inside;
        public IReadOnlyList<DungeonKey> Keys => _state.Dungeons.Keys;
        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public void UseData(GameData data)
        {
            _rules = data.Dungeons;
            _home = data.Stages;
            _enemies = data.Enemies;
            if (Inside)
                ApplyWorld();
        }

        public double RewardBonus(DungeonKey key)
        {
            var bonus = 0.0;
            foreach (var id in key.Modifiers)
            {
                if (_rules.TryGetModifier(id, out var modifier))
                    bonus += modifier.Reward;
            }
            return bonus;
        }

        public DungeonResult Enter(DungeonKey key)
        {
            if (Unlocks?.IsOpen(Feature.Dungeons) == false)
                return DungeonResult.Locked;
            if (Inside)
                return DungeonResult.AlreadyInside;
            if (_state.Rift.Inside)
                return DungeonResult.InsideRift;
            if (_stage.Linked)
                return DungeonResult.InParty;
            var tier = _rules.TierAt(key.Tier);
            if (tier == null || !_state.Dungeons.Keys.Remove(key))
                return DungeonResult.NoKey;
            var dungeons = _state.Dungeons;
            dungeons.Home = StageProgress.Capture(_state);
            dungeons.Active = key;
            dungeons.StartStage = tier.Stage + 1;
            new StageProgress { Stage = dungeons.StartStage, HighestStage = dungeons.StartStage }.Restore(_state);
            ApplyWorld();
            Respawn();
            return DungeonResult.Entered;
        }

        public void Tick()
        {
            if (!Inside)
                return;
            var dungeons = _state.Dungeons;
            if (_state.Stage >= dungeons.StartStage + _rules.Stages)
            {
                var tier = _rules.TierAt(dungeons.Active!.Tier)!;
                dungeons.Completed++;
                if (tier.Tier > dungeons.BestTier)
                    dungeons.BestTier = tier.Tier;
                _drops.GiveDungeonLoot(tier, RewardBonus(dungeons.Active));
                _events.Add(GameEvent.ForStage(GameEventKind.DungeonCompleted, _state.Tick, tier.Tier));
                Leave();
            }
            else if (_state.BossFailsInARow > 0)
            {
                dungeons.Failed++;
                _events.Add(GameEvent.ForStage(GameEventKind.DungeonFailed, _state.Tick, dungeons.Active!.Tier));
                Leave();
            }
        }

        public bool Abandon()
        {
            if (!Inside)
                return false;
            _state.Dungeons.Failed++;
            Leave();
            return true;
        }

        private void Leave()
        {
            var dungeons = _state.Dungeons;
            (dungeons.Home ?? new StageProgress()).Restore(_state);
            dungeons.Home = null;
            dungeons.Active = null;
            _stage.UseData(_home, _enemies);
            _stage.RaisesRecord = true;
            _layer.Set(System.Array.Empty<Modifier>());
            Respawn();
        }

        private void ApplyWorld()
        {
            var key = _state.Dungeons.Active!;
            var enemyHp = 1.0;
            var bossHp = 1.0;
            var timer = 1.0;
            var modifiers = new List<Modifier>();
            foreach (var id in key.Modifiers)
            {
                if (!_rules.TryGetModifier(id, out var modifier))
                    continue;
                enemyHp *= modifier.EnemyHp;
                bossHp *= modifier.BossHp;
                timer *= modifier.BossTimer;
                modifiers.AddRange(modifier.Hero);
            }
            _stage.UseData(_home.ForDungeon(enemyHp, bossHp, _rules.BossTimerSeconds * timer), _enemies);
            _stage.RaisesRecord = false;
            _layer.Set(modifiers);
        }

        private void Respawn()
        {
            _state.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            _stage.EnsureEnemy();
        }
    }
}
