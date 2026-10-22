using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class StageSystem : IKillHandler
    {
        private readonly StatSheet _hero;
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly Dictionary<string, EnemyDef?> _bosses = new Dictionary<string, EnemyDef?>();
        private readonly Dictionary<string, EnemyDef?> _guardians = new Dictionary<string, EnemyDef?>();
        private StageTable _stages = null!;
        private EnemyCatalog _enemies = null!;
        private IReadOnlyList<Modifier>? _appliedEnemyModifiers;

        public StageSystem(StageTable stages, EnemyCatalog enemies, StatSheet hero, GameState state, EventQueue events)
        {
            _hero = hero;
            _state = state;
            _events = events;
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

        public void EnsureEnemy()
        {
            if (!_state.EnemyAlive)
                SpawnNext();
        }

        public void OnEnemyKilled()
        {
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, _state.Tick, _state.Stage, _state.Enemy));
            if (_state.FightingBoss)
                AdvanceStage();
            else if (_state.KillsOnStage < _stages.EnemiesPerStage)
                _state.KillsOnStage++;
            SpawnNext();
        }

        private void AdvanceStage()
        {
            _events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, _state.Tick, _state.Stage, _state.Enemy));
            _state.Stage++;
            _state.KillsOnStage = 0;
            if (_state.Stage > _state.HighestStage)
                _state.HighestStage = _state.Stage;
        }

        private void SpawnNext()
        {
            if (_state.KillsOnStage >= _stages.EnemiesPerStage)
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
            _events.Add(GameEvent.ForEnemy(GameEventKind.BossStarted, _state.Tick, stage, enemy));
        }

        private void Spawn(EnemyDef? enemy, EnemyType type, BigNumber hp)
        {
            _state.SpawnEnemy(enemy, type, hp);
            ApplyEnemyModifiers(enemy);
            _events.Add(GameEvent.ForEnemy(GameEventKind.EnemySpawned, _state.Tick, _state.Stage, enemy));
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
