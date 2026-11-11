using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Sim.Rift;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DungeonSystem
    {
        private const int HousekeepingEveryTicks = 10;

        private readonly GameState _state;
        private readonly StageSystem _stage;
        private readonly DropSystem _drops;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private DungeonRules _rules;
        private RiftRules _riftRules;
        private StageTable _home;
        private StageTable _riftWorld;
        private EnemyCatalog _enemies;

        public DungeonSystem(GameData data, GameState state, StatSheet hero, StageSystem stage, DropSystem drops, EventQueue events)
        {
            _rules = data.Dungeons;
            _riftRules = data.Rift;
            _home = data.Stages;
            _riftWorld = RiftSystem.BuildWorld(_home, _riftRules);
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
            _riftRules = data.Rift;
            _home = data.Stages;
            _riftWorld = RiftSystem.BuildWorld(_home, _riftRules);
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

        public double RiftShards(DungeonKey key) => _rules.Rift.Shards(key.Tier) * (1 + RewardBonus(key));

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
            if (!Usable(key))
                return DungeonResult.NoKey;
            if (key.ForRift != _state.Rift.Inside)
                return key.ForRift ? DungeonResult.OutsideRift : DungeonResult.InsideRift;
            if (key.ForRift && key.Rift != _state.Rift.WindowId)
                return DungeonResult.WrongKey;
            if (!_state.Dungeons.Keys.Remove(key))
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

        public void SetAutoMerge(bool on)
        {
            _state.Dungeons.AutoMerge = on;
            if (on)
                MergeAll();
        }

        public void SetAutoSkip(string modifier, bool skip)
        {
            var skipped = _state.Dungeons.AutoSkip;
            if (!skip)
                skipped.Remove(modifier);
            else if (!skipped.Contains(modifier) && _rules.TryGetModifier(modifier, out _))
                skipped.Add(modifier);
        }

        public DungeonKey? AutoKey()
        {
            var dungeons = _state.Dungeons;
            var rift = _state.Rift.Inside;
            DungeonKey? best = null;
            foreach (var key in dungeons.Keys)
            {
                if (key.ForRift != rift || (rift && key.Rift != _state.Rift.WindowId))
                    continue;
                if (key.Tier < dungeons.AutoMinTier || key.Attempts < _rules.KeyAttempts || !Usable(key) || Skipped(key))
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
                Housekeep();
                return;
            }
            var dungeons = _state.Dungeons;
            if (_state.Stage >= dungeons.StartStage + _rules.Stages)
                Complete();
            else if (_state.BossFailsInARow > 0)
                Fail();
        }

        public bool Abandon()
        {
            if (!Inside)
                return false;
            Fail();
            return true;
        }

        public void Retreat()
        {
            if (!Inside)
                return;
            _state.Dungeons.Keys.Add(_state.Dungeons.Active!);
            Leave();
        }

        public bool RetreatIfAway(double awaySeconds)
        {
            if (!Inside || awaySeconds < _rules.RetreatSeconds)
                return false;
            Retreat();
            return true;
        }

        public DungeonKeyResult Salvage(DungeonKey key)
        {
            if (!_state.Dungeons.Keys.Remove(key))
                return DungeonKeyResult.NoKey;
            _drops.SalvageKey(key);
            return DungeonKeyResult.Done;
        }

        public DungeonKeyResult Merge(int tier, bool rift = false)
        {
            if (!rift)
            {
                var next = _rules.TierAt(tier + 1);
                if (_rules.TierAt(tier) == null || next == null || (next.NeedsLimit && _state.GatesCleared.Count == 0))
                    return DungeonKeyResult.NoHigherTier;
            }
            else if (tier < 1)
            {
                return DungeonKeyResult.NoHigherTier;
            }
            var keys = _state.Dungeons.Keys;
            var found = 0;
            foreach (var key in keys)
            {
                if (key.Tier == tier && key.ForRift == rift)
                    found++;
            }
            if (found < _rules.MergeCount)
                return DungeonKeyResult.NotEnoughKeys;
            var stage = 0L;
            var window = 0L;
            var left = _rules.MergeCount;
            for (var i = keys.Count - 1; i >= 0 && left > 0; i--)
            {
                if (keys[i].Tier != tier || keys[i].ForRift != rift)
                    continue;
                stage = System.Math.Max(stage, StageOf(keys[i]));
                window = keys[i].Rift;
                keys.RemoveAt(i);
                left--;
            }
            keys.Add(_drops.MakeKey(tier + 1, stage, window));
            _state.Dungeons.Merged++;
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonKeysMerged, _state.Tick, tier + 1));
            return DungeonKeyResult.Done;
        }

        public int MergeAll()
        {
            var merged = 0;
            var highest = 0;
            foreach (var key in _state.Dungeons.Keys)
                highest = System.Math.Max(highest, key.Tier);
            for (var tier = 1; tier <= highest; tier++)
            {
                while (Merge(tier) == DungeonKeyResult.Done)
                {
                    merged++;
                    highest = System.Math.Max(highest, tier + 1);
                }
                while (Merge(tier, true) == DungeonKeyResult.Done)
                {
                    merged++;
                    highest = System.Math.Max(highest, tier + 1);
                }
            }
            return merged;
        }

        private bool Usable(DungeonKey key) => key.ForRift ? key.Tier >= 1 : _rules.TierAt(key.Tier) != null;

        private bool Skipped(DungeonKey key)
        {
            var skipped = _state.Dungeons.AutoSkip;
            if (skipped.Count == 0)
                return false;
            foreach (var id in key.Modifiers)
            {
                if (skipped.Contains(id))
                    return true;
            }
            return false;
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
            if (_stage.Linked)
                return DungeonResult.InParty;
            return DungeonResult.Entered;
        }

        private void Housekeep()
        {
            var dungeons = _state.Dungeons;
            if (dungeons.Keys.Count == 0 || _state.Tick % HousekeepingEveryTicks != 0)
                return;
            DropKeysOfClosedRifts();
            if (dungeons.AutoMerge)
                MergeAll();
            if (!dungeons.Auto || _state.FightingBoss || Blocked() != DungeonResult.Entered)
                return;
            var key = AutoKey();
            if (key != null)
                Enter(key);
        }

        private void DropKeysOfClosedRifts()
        {
            var keys = _state.Dungeons.Keys;
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                if (keys[i].ForRift && keys[i].Rift != _state.Rift.WindowId)
                    keys.RemoveAt(i);
            }
        }

        private void Complete()
        {
            var dungeons = _state.Dungeons;
            var key = dungeons.Active!;
            dungeons.Completed++;
            if (key.ForRift)
            {
                _state.Rift.Shards += BigNumber.FromDouble(RiftShards(key));
            }
            else
            {
                var tier = _rules.TierAt(key.Tier)!;
                if (tier.Tier > dungeons.BestTier)
                    dungeons.BestTier = tier.Tier;
                _drops.GiveDungeonLoot(tier, StageOf(key), RewardBonus(key));
            }
            _events.Add(GameEvent.ForStage(GameEventKind.DungeonCompleted, _state.Tick, key.Tier));
            Leave();
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
            var rift = dungeons.Active!.ForRift;
            (dungeons.Home ?? new StageProgress()).Restore(_state);
            dungeons.Home = null;
            dungeons.Active = null;
            _stage.UseData(rift ? _riftWorld : _home, _enemies);
            _stage.RaisesRecord = !rift;
            _stage.Scripted = false;
            _layer.Set(System.Array.Empty<Modifier>());
            Respawn();
        }

        private void ApplyWorld()
        {
            var key = _state.Dungeons.Active!;
            var enemyHp = key.ForRift ? _rules.Rift.EnemyHp : _rules.DepthEnemyHp;
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
            _stage.UseData((key.ForRift ? _riftWorld : _home).ForDepth(enemyHp, bossHp, _rules.BossTimerSeconds * timer), _enemies);
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
