using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Content
{
    public sealed class StageTable
    {
        public const string FileName = "stages.json";
        public const int SupportedVersion = 1;

        private readonly ExponentialCurve _enemyHp;
        private readonly ExponentialCurve _gold;
        private readonly int _wallPeriod;
        private readonly int _wallZoneLength;
        private readonly double _wallRate;
        private readonly double _bossHpMultiplier;
        private readonly double _bossGoldMultiplier;
        private readonly string[] _biomes;

        private StageTable(ExponentialCurve enemyHp, ExponentialCurve gold, int wallPeriod, int wallZoneLength, double wallRate,
            int enemiesPerStage, double bossHpMultiplier, double bossTimerSeconds, double bossTimerMaxSeconds, double bossGoldMultiplier, int bossRetryAfterKills, int biomeLength, string[] biomes, BossHintRules bossHint)
        {
            _enemyHp = enemyHp;
            _gold = gold;
            _wallPeriod = wallPeriod;
            _wallZoneLength = wallZoneLength;
            _wallRate = wallRate;
            EnemiesPerStage = enemiesPerStage;
            _bossHpMultiplier = bossHpMultiplier;
            BossTimerSeconds = bossTimerSeconds;
            BossTimerMaxSeconds = bossTimerMaxSeconds;
            _bossGoldMultiplier = bossGoldMultiplier;
            BossRetryAfterKills = bossRetryAfterKills;
            BiomeLength = biomeLength;
            _biomes = biomes;
            BossHint = bossHint;
        }

        public int EnemiesPerStage { get; }
        public double BossTimerSeconds { get; }
        public double BossTimerMaxSeconds { get; }
        public int BossRetryAfterKills { get; }
        public int BiomeLength { get; }
        public BossHintRules BossHint { get; }
        public System.Collections.Generic.IReadOnlyList<string> Biomes => _biomes;

        public static StageTable Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var wall = root.Field("softWall");
            var period = wall.Field("period").AsInt(1);
            var zoneLength = wall.Field("zoneLength").AsInt(0);
            if (zoneLength >= period)
                throw wall.Field("zoneLength").Error($"must be shorter than period ({period})");

            var enemyHp = ExponentialCurve.Read(root.Field("enemyHp"));
            var wallRate = wall.Field("rate").AsNumber(1.0, 100.0);
            if (wallRate < enemyHp.Rate)
                throw wall.Field("rate").Error("must not be lower than enemyHp.rate, otherwise the wall makes enemies weaker");

            var boss = root.Field("boss");
            var timerSeconds = boss.Field("timerSeconds").AsNumber(1.0, 3600.0);
            var timerMaxSeconds = boss.Field("timerMaxSeconds").AsNumber(1.0, 3600.0);
            if (timerMaxSeconds < timerSeconds)
                throw boss.Field("timerMaxSeconds").Error($"must not be lower than timerSeconds ({timerSeconds})");
            var biomes = new List<string>();
            foreach (var biome in root.Field("biomes").Items())
                biomes.Add(biome.AsString());
            if (biomes.Count == 0)
                throw root.Field("biomes").Error("at least one biome is required");

            return new StageTable(
                enemyHp,
                ExponentialCurve.Read(root.Field("gold")),
                period,
                zoneLength,
                wallRate,
                root.Field("enemiesPerStage").AsInt(1, 1000),
                boss.Field("hpMultiplier").AsNumber(1.0, 1e6),
                timerSeconds,
                timerMaxSeconds,
                boss.Field("goldMultiplier").AsNumber(1.0, 1e6),
                boss.Field("retryAfterKills").AsInt(1, 1000),
                root.Field("biomeLength").AsInt(1),
                biomes.ToArray(),
                BossHintRules.Read(root.Field("bossHint")));
        }

        public StageTable ForWorld(double hpMultiplier, int wallPeriod, int wallZoneLength, double wallRate, double bossTimerSeconds) =>
            new StageTable(_enemyHp.Scaled(hpMultiplier), _gold, wallPeriod, wallZoneLength, System.Math.Max(wallRate, _enemyHp.Rate), EnemiesPerStage, _bossHpMultiplier,
                bossTimerSeconds, bossTimerSeconds, _bossGoldMultiplier, BossRetryAfterKills, BiomeLength, _biomes, BossHint);

        public BigNumber EnemyHp(long stage)
        {
            var walls = WallStagesUpTo(stage) - WallStagesUpTo(1);
            var normal = stage - 1 - walls;
            var log10 = System.Math.Log10(_enemyHp.Base) + normal * System.Math.Log10(_enemyHp.Rate) + walls * System.Math.Log10(_wallRate);
            return ExponentialCurve.FromLog10(log10);
        }

        public double BossTimer(double bonusSeconds) => System.Math.Min(BossTimerMaxSeconds, System.Math.Max(1, BossTimerSeconds + bonusSeconds));

        public double BossHpMultiplier => _bossHpMultiplier;

        public BigNumber BossHp(long stage) => EnemyHp(stage) * _bossHpMultiplier;

        public BigNumber EnemyGold(long stage) => _gold.At(stage);

        public BigNumber BossGold(long stage) => EnemyGold(stage) * _bossGoldMultiplier;

        public bool IsInSoftWall(long stage)
        {
            if (_wallZoneLength == 0)
                return false;
            var position = stage % _wallPeriod;
            return position == 0 || position > _wallPeriod - _wallZoneLength;
        }

        public string Biome(long stage) => _biomes[(int)((stage - 1) / BiomeLength % _biomes.Length)];

        public bool IsBiomeGuardian(long stage) => stage % BiomeLength == 0;

        private long WallStagesUpTo(long stage)
        {
            if (_wallZoneLength == 0)
                return 0;
            var full = stage / _wallPeriod * _wallZoneLength;
            var partial = System.Math.Max(0, stage % _wallPeriod - (_wallPeriod - _wallZoneLength));
            return full + partial;
        }
    }
}
