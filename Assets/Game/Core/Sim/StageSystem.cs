using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class StageSystem : IKillHandler
    {
        public const double StaggerHpShare = 1e-12;

        private readonly StatSheet _hero;
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly LootSystem _loot;
        private readonly LevelSystem _levels;
        private readonly Dictionary<string, EnemyDef?> _bosses = new Dictionary<string, EnemyDef?>();
        private readonly Dictionary<string, EnemyDef?> _guardians = new Dictionary<string, EnemyDef?>();
        private StageTable _stages = null!;
        private EnemyCatalog _enemies = null!;
        private IReadOnlyList<Modifier>? _appliedEnemyModifiers;

        public StageSystem(StageTable stages, EnemyCatalog enemies, StatSheet hero, GameState state, EventQueue events, LootSystem loot, LevelSystem levels)
        {
            _hero = hero;
            _state = state;
            _events = events;
            _loot = loot;
            _levels = levels;
            UseData(stages, enemies);
        }

        public void UseData(StageTable stages, EnemyCatalog enemies)
        {
            _stages = stages;
            _enemies = enemies;
            _bosses.Clear();
            _guardians.Clear();
            foreach (var biome in stages.Biomes)
            {
                _bosses[biome] = enemies.Boss(biome);
                _guardians[biome] = enemies.Guardian(biome);
            }
        }

        public bool Linked { get; private set; }
        public bool RaisesRecord { get; set; } = true;
        public IBossListener? BossListener { get; set; }
        public IKillTimeListener? KillTimeListener { get; set; }
        public DropSystem? Drops { get; set; }
        public StageTable Table => _stages;

        public void EnsureEnemy()
        {
            if (Linked && !_state.EnemyAlive)
                return;
            if (_state.EnemyAlive)
                ApplyEnemyModifiers(_state.Enemy);
            else
                SpawnNext();
        }

        public void Tick()
        {
            if (Linked)
                return;
            if (!_state.FightingBoss || !_state.EnemyAlive)
                return;
            _state.BossTimerCarry += System.Math.Max(0, _hero.Value(CombatStats.BossTimerSpeed, 1)) / System.Math.Max(1, _hero.Value(CombatStats.BossTimerSlow, 1));
            while (_state.BossTimerCarry >= 1 && _state.BossTicksLeft > 0)
            {
                _state.BossTimerCarry -= 1;
                _state.BossTicksLeft--;
            }
            if (_state.BossTicksLeft > 0)
                return;
            _state.BossTimerCarry = 0;

            _events.Add(GameEvent.ForEnemy(GameEventKind.BossFailed, _state.Tick, _state.Stage, _state.Enemy));
            _state.Farming = true;
            _state.FarmKills = 0;
            _state.BossFailsInARow++;
            _state.KillStreak = 0;
            RecordBossFraction();
            SpawnNormal();
        }

        public bool RequestBossFight()
        {
            if (Linked || !_state.Farming || _state.FightingBoss)
                return false;
            _state.Farming = false;
            SpawnBoss();
            return true;
        }

        public void OnEnemyKilled()
        {
            if (Linked)
            {
                _state.EnemyHp = _state.EnemyMaxHp * StaggerHpShare;
                return;
            }
            _state.Kills++;
            _state.KillStreak++;
            _state.TicksSinceKill = 0;
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, _state.Tick, _state.Stage, _state.Enemy));
            Reward();
            KillTimeListener?.OnKillTimed(_state.EnemyType, _state.EnemyLifeSeconds, false);
            if (_state.FightingBoss)
                AdvanceStage();
            else if (_state.Farming)
                CountFarmKill();
            else if (_state.KillsOnStage < _stages.EnemiesOnStage(_state.Stage))
                _state.KillsOnStage++;
            SpawnNext();
        }

        private void RecordBossFraction()
        {
            if (_state.EnemyMaxHp.Sign <= 0)
                return;
            var fraction = 1 - (_state.EnemyHp / _state.EnemyMaxHp).ToDouble();
            if (fraction > _state.BossBestFraction)
                _state.BossBestFraction = System.Math.Min(1, fraction);
        }

        public void Link() => Linked = true;

        public void Unlink()
        {
            if (!Linked)
                return;
            Linked = false;
            _state.FarmKills = 0;
            SpawnNext();
        }

        public BigNumber SpawnLinked(bool boss, ulong seed, double bar, int killsOnStage, bool farming, int bossTicksLeft)
        {
            var stage = _state.Stage;
            var biome = _stages.Biome(stage);
            _state.KillsOnStage = killsOnStage;
            _state.Farming = farming;
            EnemyDef? enemy;
            EnemyType type;
            BigNumber hp;
            if (boss)
            {
                var guardian = _stages.IsBiomeGuardian(stage);
                enemy = guardian ? _guardians[biome] ?? _bosses[biome] : _bosses[biome];
                type = guardian ? EnemyType.Guardian : EnemyType.Boss;
                hp = _stages.BossHp(stage) * Multiplier(enemy);
            }
            else
            {
                var normals = _enemies.Normals(biome);
                enemy = normals.Count == 0 ? null : normals[(int)(seed % (ulong)normals.Count)];
                type = EnemyType.Normal;
                hp = _stages.EnemyHp(stage) * Multiplier(enemy);
            }
            _state.SpawnEnemy(enemy, type, hp * System.Math.Max(bar, StaggerHpShare));
            _state.BossTicksLeft = boss ? bossTicksLeft : 0;
            ApplyEnemyModifiers(enemy);
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemySpawned, _state.Tick, stage, enemy));
            if (boss)
                _events.Add(GameEvent.ForEnemy(GameEventKind.BossStarted, _state.Tick, stage, enemy));
            return hp;
        }

        private void Reward()
        {
            var multiplier = _state.NextKillReward;
            _state.NextKillReward = 1;
            _loot.OnKill(_state.Stage, _state.EnemyType, _state.Enemy, multiplier);
            _levels.OnKill(_state.EnemyType, multiplier);
            Drops?.OnKill(_state.EnemyType);
            if (multiplier > 1 && _state.NextKillShardChance > 0 && _state.Rng.NextDouble() < _state.NextKillShardChance)
                _state.Rift.Shards += BigNumber.FromDouble(_state.NextKillShards);
        }

        public void ConfirmLinkedKill()
        {
            _state.Kills++;
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, _state.Tick, _state.Stage, _state.Enemy));
            Reward();
            KillTimeListener?.OnKillTimed(_state.EnemyType, _state.EnemyLifeSeconds, true);
            if (_state.FightingBoss)
                AdvanceStage();
            _state.EnemyHp = BigNumber.Zero;
        }

        public void ConfirmLinkedBossFailed()
        {
            _events.Add(GameEvent.ForEnemy(GameEventKind.BossFailed, _state.Tick, _state.Stage, _state.Enemy));
            _state.BossFailsInARow++;
            RecordBossFraction();
            _state.EnemyHp = BigNumber.Zero;
        }

        private void CountFarmKill()
        {
            _state.FarmKills++;
            if (_state.FarmKills >= _stages.BossRetryAfterKills)
                _state.Farming = false;
        }

        private void AdvanceStage()
        {
            _state.BossKills++;
            _events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, _state.Tick, _state.Stage, _state.Enemy));
            var type = _state.EnemyType;
            var stageBefore = _state.Stage;
            _state.Stage++;
            _state.KillsOnStage = 0;
            _state.Farming = false;
            _state.BossFailsInARow = 0;
            _state.BossBestFraction = 0;
            if (_state.Stage > _state.HighestStage)
                _state.HighestStage = _state.Stage;
            Drops?.OnBossDefeated(stageBefore, type, _state.RecordStage, RaisesRecord);
            if (RaisesRecord)
                _levels.OnStageReached(_state.Stage);
            BossListener?.OnBossDefeated(type, stageBefore);
        }

        private void SpawnNext()
        {
            if (!_state.Farming && _state.KillsOnStage >= _stages.EnemiesOnStage(_state.Stage))
                SpawnBoss();
            else
                SpawnNormal();
        }

        private void SpawnNormal()
        {
            var stage = _state.Stage;
            var normals = _enemies.Normals(_stages.Biome(stage));
            var enemy = normals.Count == 0 ? null : normals[_state.Rng.NextInt(0, normals.Count)];
            Spawn(enemy, EnemyType.Normal, _stages.EnemyHp(stage) * Multiplier(enemy));
        }

        private void SpawnBoss()
        {
            var stage = _state.Stage;
            var biome = _stages.Biome(stage);
            var guardian = _stages.IsBiomeGuardian(stage);
            var enemy = guardian ? _guardians[biome] ?? _bosses[biome] : _bosses[biome];
            Spawn(enemy, guardian ? EnemyType.Guardian : EnemyType.Boss, _stages.BossHp(stage) * Multiplier(enemy));
            var timer = _stages.BossTimer(_hero.Value(CombatStats.BossTimerSeconds, 0));
            _state.BossTicksLeft = (int)System.Math.Round((timer + _state.BankedSeconds) * GameLoop.TicksPerSecond);
            _state.BankedSeconds = 0;
            _state.BossTimerCarry = 0;
            _state.BossStarts++;
            _events.Add(GameEvent.ForEnemy(GameEventKind.BossStarted, _state.Tick, stage, enemy));
        }

        private void Spawn(EnemyDef? enemy, EnemyType type, BigNumber hp)
        {
            _state.SpawnEnemy(enemy, type, Weakened(hp));
            ApplyEnemyModifiers(enemy);
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemySpawned, _state.Tick, _state.Stage, enemy));
            ApplyChainDamage();
        }

        private BigNumber Weakened(BigNumber hp)
        {
            if (_state.WeakenedSpawns <= 0)
                return hp;
            var share = _state.WeakenShare;
            if (--_state.WeakenedSpawns == 0)
                _state.WeakenShare = 0;
            return hp * (1 - share);
        }

        private void ApplyChainDamage()
        {
            var damage = TakeQueueFront();
            if (damage.Sign <= 0)
                return;
            _state.PendingResource += _state.WoundedSpawnResource;
            _state.WoundedSpawnResource = 0;
            _state.EnemyHp -= damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, DamageSource.Skill, HitFlags.None));
            if (_state.EnemyAlive)
                return;
            _state.EnemyHp = BigNumber.Zero;
            OnEnemyKilled();
        }

        private BigNumber TakeQueueFront()
        {
            var queue = _state.QueueDamage;
            var front = queue[0];
            for (var i = 1; i < queue.Length; i++)
                queue[i - 1] = queue[i];
            queue[queue.Length - 1] = BigNumber.Zero;
            return front;
        }

        private void ApplyEnemyModifiers(EnemyDef? enemy)
        {
            var modifiers = enemy?.Modifiers;
            if (modifiers != null && modifiers.Count == 0)
                modifiers = null;
            if (ReferenceEquals(modifiers, _appliedEnemyModifiers))
                return;

            if (_appliedEnemyModifiers != null)
            {
                for (var i = 0; i < _appliedEnemyModifiers.Count; i++)
                    _hero.Remove(_appliedEnemyModifiers[i]);
            }
            if (modifiers != null)
            {
                for (var i = 0; i < modifiers.Count; i++)
                    _hero.Add(modifiers[i]);
            }
            _appliedEnemyModifiers = modifiers;
        }

        private static double Multiplier(EnemyDef? enemy) => enemy?.HpMultiplier ?? 1.0;
    }
}
