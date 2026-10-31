using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class StageSystem : IKillHandler
    {
        public const double StaggerHpShare = 1e-12;
        public const double MaxHpCut = 0.9;

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
        private int _ownKillsOnStage;
        private bool _ownFarming;
        private int _ownFarmKills;

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

        private double WithClockwork(double rate) =>
            _state.ClockworkSlow > 0 ? System.Math.Min(rate, System.Math.Max(_state.ClockworkFloor, rate * (1 - _state.ClockworkSlow))) : rate;

        public void Tick()
        {
            if (Linked)
                return;
            TickGolden();
            Regenerate();
            StripShield();
            if (!_state.FightingBoss || !_state.EnemyAlive)
                return;
            if (_state.BossTimerStopTicks > 0)
            {
                _state.BossTimerStopTicks--;
                _state.BossTimerRate = 0;
                return;
            }
            _state.BossTimerRate = WithClockwork(System.Math.Max(0, _hero.Value(CombatStats.BossTimerSpeed, 1)) / System.Math.Max(1, _hero.Value(CombatStats.BossTimerSlow, 1)));
            _state.BossTimerCarry += _state.BossTimerRate;
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
            NoteWeakenedKill();
            _state.KillStreak++;
            _state.TicksSinceKill = 0;
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, _state.Tick, _state.Stage, _state.Enemy));
            Reward();
            KillTimeListener?.OnKillTimed(_state.EnemyType, _state.EnemyLifeSeconds, false);
            if (_state.FightingBoss)
                AdvanceStage();
            else if (_state.Farming)
                CountFarmKill();
            else if (_state.KillsOnStage < EnemiesOnStage(_state.Stage))
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

        public void Link(bool asGuest = false)
        {
            Linked = true;
            LinkedAsGuest = asGuest;
            if (!asGuest)
                return;
            _ownKillsOnStage = _state.KillsOnStage;
            _ownFarming = _state.Farming;
            _ownFarmKills = _state.FarmKills;
        }

        public bool LinkedAsGuest { get; private set; }

        public void Unlink()
        {
            if (!Linked)
                return;
            Linked = false;
            _state.FarmKills = 0;
            _state.PartyWaveStage = 0;
            _state.PartyEnemiesOnStage = 0;
            _state.EnemyShieldFloor = BigNumber.Zero;
            if (LinkedAsGuest)
            {
                _state.KillsOnStage = _ownKillsOnStage;
                _state.Farming = _ownFarming;
                _state.FarmKills = _ownFarmKills;
                LinkedAsGuest = false;
            }
            SpawnNext();
        }

        public BigNumber SpawnLinked(bool boss, ulong seed, double bar, int killsOnStage, bool farming, int bossTicksLeft, bool weakened = false, double weakenedGold = 0, long waveStage = 0)
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
            _state.EnemyWeakened = weakened;
            _state.EnemyGoldBonus = weakened ? weakenedGold : 0;
            _state.PartyWaveStage = waveStage;
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
            _loot.OnKill(_state.Stage, _state.EnemyType, _state.Enemy, multiplier * (1 + _state.EnemyGoldBonus));
            _levels.OnKill(_state.EnemyType, multiplier);
            Drops?.OnKill(_state.EnemyType);
            if (multiplier > 1 && _state.NextKillShardChance > 0 && _state.Rng.NextDouble() < _state.NextKillShardChance)
                _state.Rift.Shards += BigNumber.FromDouble(_state.NextKillShards);
        }

        private void NoteWeakenedKill()
        {
            if (!_state.EnemyWeakened)
                return;
            _state.WeakenedKills++;
            _state.LastWeakenedKillSeconds = _state.EnemyLifeSeconds;
        }

        public void ConfirmLinkedKill()
        {
            _state.Kills++;
            NoteWeakenedKill();
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, _state.Tick, _state.Stage, _state.Enemy));
            Reward();
            KillTimeListener?.OnKillTimed(_state.EnemyType, _state.EnemyLifeSeconds, true);
            if (_state.FightingBoss)
                AdvanceStage();
            _state.EnemyHp = BigNumber.Zero;
        }

        public void SkipLinkedKill() => _state.EnemyHp = BigNumber.Zero;

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
            var wanderer = _state.WandererBoss;
            _state.Stage++;
            _state.KillsOnStage = 0;
            _state.Farming = false;
            _state.BossFailsInARow = 0;
            _state.BossBestFraction = 0;
            if (_state.Stage > _state.HighestStage)
                _state.HighestStage = _state.Stage;
            if (wanderer)
                Jump();
            Drops?.OnBossDefeated(stageBefore, type, _state.RecordStage, RaisesRecord);
            if (Gates?.TryClear(stageBefore) is StageGate gate)
            {
                if (gate.FirstKillUnique)
                    Drops?.GiveLimitUnique(stageBefore);
                _levels.SyncTreePoints();
            }
            OpenChest(stageBefore);
            if (RaisesRecord)
                _levels.OnStageReached(_state.Stage);
            BossListener?.OnBossDefeated(type, stageBefore);
        }

        public double ChestGold { get; set; }
        public GateSystem? Gates { get; set; }

        private void OpenChest(long stage)
        {
            var every = (long)System.Math.Round(_hero.Value(CombatStats.ChestEvery, 0));
            if (every >= 1 && stage % every == 0)
                _loot.Chest(stage, ChestGold);
        }

        private void SpawnNext()
        {
            if (!_state.Farming && _state.KillsOnStage >= EnemiesOnStage(_state.Stage))
                SpawnBoss();
            else
                SpawnNormal();
        }

        private void SpawnNormal()
        {
            var stage = _state.Stage;
            var normals = _enemies.Normals(_stages.Biome(stage));
            var enemy = normals.Count == 0 ? null : normals[_state.Rng.NextInt(0, normals.Count)];
            var cut = System.Math.Min(MaxHpCut, System.Math.Max(0, _hero.Value(CombatStats.EnemyHpCut, 0)));
            if (GoldenComes(stage))
            {
                Spawn(enemy, EnemyType.Normal, _stages.EnemyHp(stage) * (Multiplier(enemy) * Encounters.GoldenHpMultiplier));
                _state.GoldenTicks = (int)System.Math.Round(Encounters.GoldenSeconds * GameLoop.TicksPerSecond);
                _state.GoldenNormalHp = _stages.EnemyHp(stage) * Multiplier(enemy);
                _events.Add(GameEvent.ForStage(GameEventKind.GoldenColossusAppeared, _state.Tick, stage));
                return;
            }
            Spawn(enemy, EnemyType.Normal, _stages.EnemyHp(stage) * (Multiplier(enemy) * (1 - cut)));
        }

        public EncounterRules Encounters { get; set; } = null!;

        private bool GoldenComes(long stage) =>
            RaisesRecord && Encounters.GoldenChance > 0 && stage >= Encounters.GoldenMinStage && _state.Rng.NextDouble() < Encounters.GoldenChance;

        private bool WandererComes(long stage, bool guardian) =>
            RaisesRecord && !guardian && WandererChance > 0 && stage >= Encounters.WandererMinStage
            && stage + Encounters.JumpMin <= _state.RecordStage && _state.Rng.NextDouble() < WandererChance;

        private double WandererChance => Encounters.WandererChance + System.Math.Max(0, _hero.Value(CombatStats.WandererChance, 0));

        private void TickGolden()
        {
            if (_state.GoldenTicks <= 0)
                return;
            if (_state.GoldenDamage.Sign > 0 && _state.GoldenNormalHp.Sign > 0)
            {
                _loot.Golden(_state.Stage, (_state.GoldenDamage / _state.GoldenNormalHp).ToDouble() * Encounters.GoldenGold);
                _state.GoldenDamage = BigNumber.Zero;
            }
            if (--_state.GoldenTicks > 0 || !_state.EnemyAlive)
                return;
            SpawnNormal();
        }

        private void Jump()
        {
            var jump = _state.Rng.NextInt(Encounters.JumpMin, Encounters.JumpMax + 1);
            var target = System.Math.Min(System.Math.Min(_state.Stage + jump, _state.RecordStage), _stages.NextGateFrom(_state.Stage + 1));
            if (target <= _state.Stage)
                return;
            var jumped = target - _state.Stage;
            var skipped = 0L;
            for (var stage = _state.Stage; stage < target; stage++)
                skipped += _stages.EnemiesOnStage(stage);
            _levels.OnSkipped(skipped, jumped);
            _state.Stage = target;
            if (_state.Stage > _state.HighestStage)
                _state.HighestStage = _state.Stage;
            _events.Add(GameEvent.WithAmount(GameEventKind.WandererJumped, _state.Tick, BigNumber.FromDouble(jumped), _state.Stage));
        }

        private void SpawnBoss()
        {
            var stage = _state.Stage;
            var biome = _stages.Biome(stage);
            var gate = _stages.GateAt(stage);
            var guardian = gate != null || _stages.IsBiomeGuardian(stage);
            var enemy = guardian ? _guardians[biome] ?? _bosses[biome] : _bosses[biome];
            var guardianShare = guardian && gate == null ? System.Math.Max(0.1, _hero.Value(CombatStats.GuardianHp, 1)) : 1;
            var gateShare = gate?.HpMultiplier ?? 1;
            var bossHp = _stages.BossHp(stage) * (Multiplier(enemy) * guardianShare * gateShare);
            var shield = System.Math.Max(0, _hero.Value(CombatStats.BossShield, 0));
            Spawn(enemy, guardian ? EnemyType.Guardian : EnemyType.Boss, bossHp * (1 + shield));
            if (shield > 0)
                _state.EnemyShieldFloor = bossHp;
            _state.WandererBoss = gate == null && WandererComes(stage, guardian);
            var timer = gate?.TimerSeconds ?? _stages.BossTimer(_hero.Value(CombatStats.BossTimerSeconds, 0));
            _state.BossTicksLeft = (int)System.Math.Round((timer + _state.BankedSeconds) * GameLoop.TicksPerSecond);
            _state.BankedSeconds = 0;
            _state.BossTimerCarry = 0;
            _events.Add(GameEvent.ForEnemy(GameEventKind.BossStarted, _state.Tick, stage, enemy));
        }

        private void Spawn(EnemyDef? enemy, EnemyType type, BigNumber hp)
        {
            var spread = _state.EnemyAlive ? 0 : System.Math.Min(1, System.Math.Max(0, _hero.Value(CombatStats.DotSpread, 0, StatTags.DoT)));
            var poison = _state.PoisonStacks * (!_state.EnemyAlive && _hero.Value(CombatStats.PoisonNoDecay, 0, StatTags.DoT) >= 1 ? 1 : spread);
            var burn = _state.BurnStacks * spread;
            var weakened = _state.WeakenedSpawns > 0;
            var goldBonus = weakened ? _state.WeakenGoldBonus : 0;
            var vulnerability = weakened ? _state.WeakenVulnerabilityShare : 0;
            var vulnerabilityTicks = _state.WeakenVulnerabilityTicks;
            _state.SpawnEnemy(enemy, type, Weakened(hp));
            _state.EnemyWeakened = weakened;
            _state.EnemyGoldBonus = goldBonus;
            if (vulnerability > 0)
            {
                _state.VulnerabilityShare = System.Math.Max(_state.VulnerabilityShare, vulnerability);
                _state.VulnerabilityTicks = System.Math.Max(_state.VulnerabilityTicks, vulnerabilityTicks);
            }
            _state.PoisonStacks = poison;
            _state.BurnStacks = burn;
            ApplyEnemyModifiers(enemy);
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemySpawned, _state.Tick, _state.Stage, enemy));
            ApplyChainDamage();
        }

        public int EnemiesOnThisStage => Linked && _state.PartyEnemiesOnStage > 0 ? _state.PartyEnemiesOnStage : EnemiesOnStage(_state.Stage);

        public double ShieldShare => _state.EnemyShieldFloor.Sign > 0 && _state.EnemyMaxHp.Sign > 0 ? System.Math.Min(1, (_state.EnemyShieldFloor / _state.EnemyMaxHp).ToDouble()) : 0;

        private int EnemiesOnStage(long stage) =>
            (int)System.Math.Floor(_stages.EnemiesOnStage(stage) * System.Math.Max(1, _hero.Value(CombatStats.StageEnemies, 1)) + 1e-9);

        private void Regenerate()
        {
            var regen = _hero.Value(CombatStats.EnemyRegen, 0);
            if (regen > 0 && _state.EnemyAlive)
                _state.EnemyHp = BigNumber.Min(_state.EnemyMaxHp, _state.EnemyHp + _state.EnemyMaxHp * (regen * GameLoop.TickSeconds));
        }

        private void StripShield()
        {
            if (_state.EnemyShieldFloor.Sign <= 0 || _hero.Value(CombatStats.BossShield, 0) > 0)
                return;
            _state.EnemyHp = BigNumber.Min(_state.EnemyHp, _state.EnemyShieldFloor);
            _state.EnemyShieldFloor = BigNumber.Zero;
        }

        private BigNumber Weakened(BigNumber hp)
        {
            if (_state.WeakenedSpawns <= 0)
                return hp;
            var share = _state.WeakenShare;
            if (--_state.WeakenedSpawns == 0)
            {
                _state.WeakenShare = 0;
                _state.WeakenGoldBonus = 0;
                _state.WeakenVulnerabilityShare = 0;
                _state.WeakenVulnerabilityTicks = 0;
            }
            return hp * (1 - share);
        }

        public BigNumber TakeLinkedChainDamage()
        {
            var damage = TakeQueueFront();
            if (damage.Sign <= 0)
                return BigNumber.Zero;
            _state.PendingResource += _state.WoundedSpawnResource;
            _state.WoundedSpawnResource = 0;
            _events.Add(GameEvent.Damage(_state.Tick, damage, DamageSource.Skill, HitFlags.None));
            return damage;
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
