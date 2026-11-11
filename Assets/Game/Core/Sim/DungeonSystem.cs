using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim.Rift;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DungeonSystem
    {
        private const int AutoEveryTicks = 10;

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
            RefillOldKeys();
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

        public long StageOf(DungeonKey key) => key.Stage > 0 ? key.Stage : _rules.TierAt(key.Tier)?.Stage ?? 1;

        public long StartStage(DungeonKey key)
        {
            var start = StageOf(key) + 1;
            for (var stage = start + _rules.Stages - 1; stage >= start; stage--)
            {
                if (_home.IsBiomeGuardian(stage))
                    return stage + 1;
            }
            return start;
        }

        public DungeonResult Enter(DungeonKey key)
        {
            var blocked = Blocked();
            if (blocked != DungeonResult.Entered)
                return blocked;
            var tier = _rules.TierAt(key.Tier);
            if (tier == null || !_state.Dungeons.Keys.Remove(key))
                return DungeonResult.NoKey;
            var dungeons = _state.Dungeons;
            dungeons.Home = StageProgress.Capture(_state);
            dungeons.Active = key;
            dungeons.StartStage = StartStage(key);
            new StageProgress { Stage = dungeons.StartStage, HighestStage = dungeons.StartStage }.Restore(_state);
            ApplyWorld();
            Respawn();
            return DungeonResult.Entered;
        }

        public void SetAuto(bool on, int minTier)
        {
            _state.Dungeons.Auto = on;
            _state.Dungeons.AutoMinTier = System.Math.Max(1, minTier);
        }

        public DungeonKey? AutoKey()
        {
            var dungeons = _state.Dungeons;
            DungeonKey? best = null;
            foreach (var key in dungeons.Keys)
            {
                if (key.Tier < dungeons.AutoMinTier || key.Attempts < _rules.KeyAttempts || _rules.TierAt(key.Tier) == null)
                    continue;
                if (best == null || key.Tier > best.Tier || (key.Tier == best.Tier && StageOf(key) > StageOf(best)))
                    best = key;
            }
            return best;
        }

        public void Tick()
        {
            if (!Inside)
            {
                EnterAutomatically();
                return;
            }
            var dungeons = _state.Dungeons;
            if (_state.Stage >= dungeons.StartStage + _rules.Stages)
            {
                var key = dungeons.Active!;
                var tier = _rules.TierAt(key.Tier)!;
                dungeons.Completed++;
                if (tier.Tier > dungeons.BestTier)
                    dungeons.BestTier = tier.Tier;
                _drops.GiveDungeonLoot(tier, StageOf(key), RewardBonus(key));
                _events.Add(GameEvent.ForStage(GameEventKind.DungeonCompleted, _state.Tick, tier.Tier));
                Leave();
            }
            else if (_state.BossFailsInARow > 0)
            {
                Fail();
            }
        }

        public bool Abandon()
        {
            if (!Inside)
                return false;
            Fail();
            return true;
        }

        public bool RetreatIfAway(double awaySeconds)
        {
            if (!Inside || awaySeconds < _rules.RetreatSeconds)
                return false;
            _state.Dungeons.Keys.Add(_state.Dungeons.Active!);
            Leave();
            return true;
        }

        public DungeonKeyResult Salvage(DungeonKey key)
        {
            if (!_state.Dungeons.Keys.Remove(key))
                return DungeonKeyResult.NoKey;
            _drops.SalvageKey(key.Tier);
            return DungeonKeyResult.Done;
        }

        public DungeonKeyResult Merge(int tier)
        {
            var next = _rules.TierAt(tier + 1);
            if (_rules.TierAt(tier) == null || next == null || (next.NeedsLimit && _state.GatesCleared.Count == 0))
                return DungeonKeyResult.NoHigherTier;
            var keys = _state.Dungeons.Keys;
            var found = 0;
            foreach (var key in keys)
            {
                if (key.Tier == tier)
                    found++;
            }
            if (found < _rules.MergeCount)
                return DungeonKeyResult.NotEnoughKeys;
            var stage = 0L;
            var left = _rules.MergeCount;
            for (var i = keys.Count - 1; i >= 0 && left > 0; i--)
            {
                if (keys[i].Tier != tier)
                    continue;
                stage = System.Math.Max(stage, StageOf(keys[i]));
                keys.RemoveAt(i);
                left--;
            }
            keys.Add(_drops.MakeKey(next.Tier, stage));
            _state.Dungeons.Merged++;
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonKeysMerged, _state.Tick, next.Tier));
            return DungeonKeyResult.Done;
        }

        private void RefillOldKeys()
        {
            var dungeons = _state.Dungeons;
            foreach (var key in dungeons.Keys)
            {
                if (key.Attempts <= 0)
                    key.Attempts = _rules.KeyAttempts;
            }
            if (dungeons.Active != null && dungeons.Active.Attempts <= 0)
                dungeons.Active.Attempts = _rules.KeyAttempts;
        }

        private DungeonResult Blocked()
        {
            if (Unlocks?.IsOpen(Feature.Dungeons) == false)
                return DungeonResult.Locked;
            if (Inside || _state.Events.FightInside)
                return DungeonResult.AlreadyInside;
            if (_state.Rift.Inside)
                return DungeonResult.InsideRift;
            if (_stage.Linked)
                return DungeonResult.InParty;
            return DungeonResult.Entered;
        }

        private void EnterAutomatically()
        {
            if (!_state.Dungeons.Auto || _state.FightingBoss || _state.Tick % AutoEveryTicks != 0 || _state.Dungeons.Keys.Count == 0)
                return;
            if (Blocked() != DungeonResult.Entered)
                return;
            var key = AutoKey();
            if (key != null)
                Enter(key);
        }

        private void Fail()
        {
            var dungeons = _state.Dungeons;
            var key = dungeons.Active!;
            dungeons.Failed++;
            key.Attempts--;
            if (key.Attempts > 0)
                dungeons.Keys.Add(key);
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonFailed, _state.Tick, key.Tier));
            Leave();
        }

        private void Leave()
        {
            var dungeons = _state.Dungeons;
            (dungeons.Home ?? new StageProgress()).Restore(_state);
            dungeons.Home = null;
            dungeons.Active = null;
            _stage.UseData(_home, _enemies);
            _stage.RaisesRecord = true;
            _stage.Scripted = false;
            _layer.Set(System.Array.Empty<Modifier>());
            Respawn();
        }

        private void ApplyWorld()
        {
            var key = _state.Dungeons.Active!;
            var enemyHp = _rules.DepthEnemyHp;
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
            _stage.UseData(_home.ForDepth(enemyHp, bossHp, _rules.BossTimerSeconds * timer), _enemies);
            _stage.RaisesRecord = false;
            _stage.Scripted = true;
            _layer.Set(modifiers);
        }

        private void Respawn()
        {
            _state.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            _stage.EnsureEnemy();
        }
    }
}
