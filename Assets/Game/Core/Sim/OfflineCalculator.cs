using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class OfflineCalculator
    {
        private readonly GameData _data;
        private readonly StatSheet _hero;
        private readonly Dictionary<string, (double Hp, double Gold)> _normalAverages = new Dictionary<string, (double, double)>();

        public OfflineCalculator(GameData data, StatSheet hero)
        {
            _data = data;
            _hero = hero;
            foreach (var biome in data.Stages.Biomes)
                _normalAverages[biome] = Averages(data.Enemies.Normals(biome));
        }

        public BigNumber OfflineDps(GameState state) => state.RecentDps * System.Math.Min(1, _data.Combat.Offline.DpsShare + System.Math.Max(0, _hero.Value(CombatStats.OfflineDpsShare, 0)));

        public OfflineResult Simulate(GameState state, double seconds) => Simulate(state, seconds, OfflineDps(state), fightGuardians: false);

        public OfflineResult Simulate(GameState state, double seconds, BigNumber dps, bool fightGuardians)
        {
            var stages = _data.Stages;
            var capSeconds = _data.Combat.Offline.CapSeconds + _hero.Value(CombatStats.OfflineCapHours, 0) * 3600;
            var time = System.Math.Max(0, System.Math.Min(seconds, capSeconds));
            var used = time;
            var stage = state.Stage;
            var kills = state.Farming ? stages.EnemiesOnStage(stage) : System.Math.Min(state.KillsOnStage, stages.EnemiesOnStage(stage));
            var farming = state.Farming;
            var stoppedAtGuardian = false;
            var stuckAtBoss = false;
            var totalKills = 0L;
            var gold = BigNumber.Zero;
            var goldMultiplier = _hero.Compute(CombatStats.Gold, BigNumber.One, StatTags.Gold) * _hero.Value(CombatStats.OfflineGold, 1, StatTags.Gold);

            if (dps.Sign <= 0 || time <= 0 || state.Rift.Inside)
                return new OfflineResult(0, 0, BigNumber.Zero, stage, stage, kills, farming, false, false);

            while (!farming)
            {
                var normal = NormalEnemy(stage, dps, goldMultiplier);
                var remaining = stages.EnemiesOnStage(stage) - kills;
                if (time < remaining * normal.Seconds)
                {
                    var partial = (int)System.Math.Floor(time / normal.Seconds);
                    kills += partial;
                    totalKills += partial;
                    gold += normal.Gold * partial;
                    time = 0;
                    break;
                }
                time -= remaining * normal.Seconds;
                totalKills += remaining;
                gold += normal.Gold * remaining;
                kills = stages.EnemiesOnStage(stage);

                var guardian = stages.IsBiomeGuardian(stage);
                if (guardian && !fightGuardians)
                {
                    stoppedAtGuardian = true;
                    farming = true;
                    break;
                }

                var boss = BossEnemy(stage, guardian, dps, goldMultiplier);
                if (boss.Seconds > BossTimer)
                {
                    stuckAtBoss = true;
                    farming = true;
                    break;
                }
                if (time < boss.Seconds)
                {
                    time = 0;
                    break;
                }
                time -= boss.Seconds;
                totalKills++;
                gold += boss.Gold;
                stage++;
                kills = 0;
            }

            if (farming && time > 0)
            {
                var normal = NormalEnemy(stage, dps, goldMultiplier);
                var farmed = FarmKills(time, normal.Seconds, retriesBoss: !stoppedAtGuardian);
                totalKills += farmed;
                gold += normal.Gold * farmed;
            }

            return new OfflineResult(used, totalKills, gold, state.Stage, stage, kills, farming, stoppedAtGuardian, stuckAtBoss);
        }

        public static void Apply(GameState state, OfflineResult result)
        {
            if (result.StageAfter != result.StageBefore)
            {
                state.BossFailsInARow = 0;
                state.BossBestFraction = 0;
            }
            state.Stage = result.StageAfter;
            if (state.Stage > state.HighestStage)
                state.HighestStage = state.Stage;
            state.KillsOnStage = result.KillsOnStage;
            state.Farming = result.Farming;
            state.FarmKills = 0;
            state.Gold += result.Gold;
            state.RunGoldEarned += result.Gold;
            if (result.Kills > 0 || result.StageAfter != result.StageBefore)
                state.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
        }

        private double BossTimer => _data.Stages.BossTimer(_hero.Value(CombatStats.BossTimerSeconds, 0));

        private long FarmKills(double time, double killSeconds, bool retriesBoss)
        {
            if (!retriesBoss)
                return (long)System.Math.Floor(time / killSeconds);

            var killsPerCycle = _data.Stages.BossRetryAfterKills;
            var cycleSeconds = killsPerCycle * killSeconds + BossTimer;
            var cycles = System.Math.Floor(time / cycleSeconds);
            var rest = time - cycles * cycleSeconds;
            var restKills = System.Math.Min(killsPerCycle, System.Math.Floor(rest / killSeconds));
            return (long)(cycles * killsPerCycle + restKills);
        }

        private (double Seconds, BigNumber Gold) NormalEnemy(long stage, BigNumber dps, BigNumber goldMultiplier)
        {
            var average = _normalAverages[_data.Stages.Biome(stage)];
            var hp = _data.Stages.EnemyHp(stage) * average.Hp;
            return (KillSeconds(hp, dps), _data.Stages.EnemyGold(stage) * average.Gold * goldMultiplier);
        }

        private (double Seconds, BigNumber Gold) BossEnemy(long stage, bool guardian, BigNumber dps, BigNumber goldMultiplier)
        {
            var biome = _data.Stages.Biome(stage);
            var enemy = guardian ? _data.Enemies.Guardian(biome) ?? _data.Enemies.Boss(biome) : _data.Enemies.Boss(biome);
            var hp = _data.Stages.BossHp(stage) * (enemy?.HpMultiplier ?? 1.0);
            return (KillSeconds(hp, dps), _data.Stages.BossGold(stage) * (enemy?.GoldMultiplier ?? 1.0) * goldMultiplier);
        }

        private static double KillSeconds(BigNumber hp, BigNumber dps) => System.Math.Max(GameLoop.TickSeconds, (hp / dps).ToDouble());

        private static (double Hp, double Gold) Averages(IReadOnlyList<EnemyDef> normals)
        {
            if (normals.Count == 0)
                return (1.0, 1.0);
            var hp = 0.0;
            var gold = 0.0;
            foreach (var enemy in normals)
            {
                hp += enemy.HpMultiplier;
                gold += enemy.GoldMultiplier;
            }
            return (hp / normals.Count, gold / normals.Count);
        }
    }
}
