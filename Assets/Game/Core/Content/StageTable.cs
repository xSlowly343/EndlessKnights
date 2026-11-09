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
        private readonly WaveRules _waves;
        private (long From, double Log10Rate)[] _hpSteps = System.Array.Empty<(long, double)>();
        private bool _wallsOnTop;
        private (long From, double Log10Rate)[] _goldSteps = System.Array.Empty<(long, double)>();

        private StageTable(ExponentialCurve enemyHp, ExponentialCurve gold, int wallPeriod, int wallZoneLength, double wallRate,
            WaveRules waves, double bossHpMultiplier, double bossTimerSeconds, double bossTimerMaxSeconds, double bossGoldMultiplier, int bossRetryAfterKills, int biomeLength, string[] biomes, BossHintRules bossHint)
        {
            _enemyHp = enemyHp;
            _gold = gold;
            _wallPeriod = wallPeriod;
            _wallZoneLength = wallZoneLength;
            _wallRate = wallRate;
            _waves = waves;
            _bossHpMultiplier = bossHpMultiplier;
            BossTimerSeconds = bossTimerSeconds;
            BossTimerMaxSeconds = bossTimerMaxSeconds;
            _bossGoldMultiplier = bossGoldMultiplier;
            BossRetryAfterKills = bossRetryAfterKills;
            BiomeLength = biomeLength;
            _biomes = biomes;
            BossHint = bossHint;
        }

        public int WaveCount => _waves.Count;
        public int EnemiesPerStage => EnemiesOnStage(1);
        public WaveRules Waves => _waves;
        public double BossTimerSeconds { get; }
        public double BossTimerMaxSeconds { get; }
        public int BossRetryAfterKills { get; }
        public int BiomeLength { get; }
        public BossHintRules BossHint { get; }
        public System.Collections.Generic.IReadOnlyList<string> Biomes => _allBiomes;
        public string Heart { get; private set; } = string.Empty;
        public long HeartFromStage { get; private set; }
        public int BossSlotStages { get; private set; } = 10;
        public double RotationMinutes { get; private set; }
        public bool Rotates => RotationMinutes > 0 && _allBiomes.Length > 2;
        public System.Collections.Generic.IReadOnlyList<string> Rotation => _biomes;
        public System.Collections.Generic.IReadOnlyList<string> Pool => _allBiomes;
        private string[] _allBiomes = System.Array.Empty<string>();
        private long[] _opensAt = System.Array.Empty<long>();
        public StageGate[] Gates { get; private set; } = System.Array.Empty<StageGate>();

        public StageGate? GateAt(long stage)
        {
            for (var i = 0; i < Gates.Length; i++)
            {
                if (Gates[i].Stage == stage)
                    return Gates[i];
            }
            return null;
        }

        public long NextGateFrom(long stage)
        {
            var next = long.MaxValue;
            for (var i = 0; i < Gates.Length; i++)
            {
                if (Gates[i].Stage >= stage && Gates[i].Stage < next)
                    next = Gates[i].Stage;
            }
            return next;
        }

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
            var wallExtra = wall.HasField("extra") ? wall.Field("extra").AsNumber(1.0, 100.0) : 0;
            var wallRate = wallExtra > 0 ? wallExtra : wall.Field("rate").AsNumber(1.0, 100.0);
            if (wallExtra <= 0 && wallRate < enemyHp.Rate)
                throw wall.Field("rate").Error("must not be lower than enemyHp.rate, otherwise the wall makes enemies weaker");

            var boss = root.Field("boss");
            var timerSeconds = boss.Field("timerSeconds").AsNumber(1.0, 3600.0);
            var timerMaxSeconds = boss.Field("timerMaxSeconds").AsNumber(1.0, 3600.0);
            if (timerMaxSeconds < timerSeconds)
                throw boss.Field("timerMaxSeconds").Error($"must not be lower than timerSeconds ({timerSeconds})");
            var biomes = new List<string>();
            var opensAt = new List<long>();
            foreach (var biome in root.Field("biomes").Items())
            {
                var from = biome.HasField("fromStage") ? biome.Field("fromStage").AsLong(1) : 1;
                if (opensAt.Count > 0 && from < opensAt[opensAt.Count - 1])
                    throw biome.Field("fromStage").Error("biomes must open in stage order");
                biomes.Add(biome.HasField("id") ? biome.Field("id").AsString() : biome.AsString());
                opensAt.Add(from);
            }
            if (opensAt.Count > 0 && opensAt[0] != 1)
                throw root.Field("biomes").Error("the first biome must open at stage 1");
            if (biomes.Count == 0)
                throw root.Field("biomes").Error("at least one biome is required");
            var heart = string.Empty;
            var heartFrom = 0L;
            if (root.HasField("heart"))
            {
                heart = root.Field("heart").Field("biome").AsString();
                heartFrom = root.Field("heart").Field("fromStage").AsLong(1);
                if (biomes.Contains(heart))
                    throw root.Field("heart").Field("biome").Error($"'{heart}' is already in the biome rotation");
            }
            var allBiomes = new List<string>(biomes);
            if (heart.Length > 0)
            {
                allBiomes.Add(heart);
                opensAt.Add(heartFrom);
            }

            var gates = new List<StageGate>();
            if (root.HasField("gates"))
            {
                foreach (var item in root.Field("gates").Items())
                    gates.Add(StageGate.Read(item));
            }
            var hpSteps = ReadSteps(root.Field("enemyHp"), enemyHp);
            var goldCurve = ExponentialCurve.Read(root.Field("gold"));
            var goldSteps = ReadSteps(root.Field("gold"), goldCurve);
            return new StageTable(
                enemyHp,
                goldCurve,
                period,
                zoneLength,
                wallRate,
                WaveRules.Read(root.Field("waves")),
                boss.Field("hpMultiplier").AsNumber(1.0, 1e6),
                timerSeconds,
                timerMaxSeconds,
                boss.Field("goldMultiplier").AsNumber(1.0, 1e6),
                boss.Field("retryAfterKills").AsInt(1, 1000),
                root.Field("biomeLength").AsInt(1),
                biomes.ToArray(),
                BossHintRules.Read(root.Field("bossHint")))
            {
                Gates = gates.ToArray(),
                ItemLevelRate = root.Field("enemyHp").HasField("itemLevelRate") ? root.Field("enemyHp").Field("itemLevelRate").AsNumber(1, 2) : 1,
                _wallsOnTop = wallExtra > 0,
                _hpSteps = hpSteps,
                _goldSteps = goldSteps,
                Heart = heart,
                HeartFromStage = heartFrom,
                RotationMinutes = root.HasField("rotation") ? root.Field("rotation").Field("minutes").AsNumber(0.1, 1440) : 0,
                BossSlotStages = boss.HasField("slotStages") ? boss.Field("slotStages").AsInt(1, 1000) : 10,
                _allBiomes = allBiomes.ToArray(),
                _opensAt = opensAt.ToArray()
            };
        }

        private static (long, double)[] ReadSteps(ContentNode node, ExponentialCurve curve)
        {
            var steps = new List<(long, double)> { (1, System.Math.Log10(curve.Rate)) };
            if (!node.HasField("steps"))
                return steps.ToArray();
            foreach (var item in node.Field("steps").Items())
            {
                var from = item.Field("from").AsLong(2);
                if (from <= steps[steps.Count - 1].Item1)
                    throw item.Field("from").Error("steps must go up by stage");
                steps.Add((from, System.Math.Log10(item.Field("rate").AsNumber(1.0, 100.0))));
            }
            return steps.ToArray();
        }

        public StageTable ForWorld(double hpMultiplier, int wallPeriod, int wallZoneLength, double wallRate, double bossTimerSeconds) =>
            new StageTable(_enemyHp.Scaled(hpMultiplier), _gold, wallPeriod, wallZoneLength, System.Math.Max(wallRate, _enemyHp.Rate), _waves, _bossHpMultiplier,
                bossTimerSeconds, bossTimerSeconds, _bossGoldMultiplier, BossRetryAfterKills, BiomeLength, _biomes, BossHint)
            {
                Gates = Gates,
                _hpSteps = _hpSteps,
                _goldSteps = _goldSteps,
                Heart = Heart,
                HeartFromStage = HeartFromStage,
                RotationMinutes = RotationMinutes,
                BossSlotStages = BossSlotStages,
                _allBiomes = _allBiomes,
                _opensAt = _opensAt
            };


        public StageTable ForDungeon(double hpMultiplier, double bossHpMultiplier, double bossTimerSeconds) =>
            new StageTable(_enemyHp.Scaled(hpMultiplier), _gold, _wallPeriod, 0, _wallRate, _waves, _bossHpMultiplier * bossHpMultiplier,
                bossTimerSeconds, bossTimerSeconds, _bossGoldMultiplier, BossRetryAfterKills, BiomeLength, _biomes, BossHint)
            {
                _hpSteps = _hpSteps,
                _goldSteps = _goldSteps,
                Heart = Heart,
                HeartFromStage = HeartFromStage,
                RotationMinutes = RotationMinutes,
                BossSlotStages = BossSlotStages,
                _allBiomes = _allBiomes,
                _opensAt = _opensAt
            };

        public int WaveSize(long stage, int wave) => _waves.Size(stage, wave);

        public int EnemiesOnStage(long stage)
        {
            var total = 0;
            for (var wave = 0; wave < _waves.Count; wave++)
                total += _waves.Size(stage, wave);
            return total;
        }

        public double NormalShare(long stage)
        {
            var steps = EnemiesOnStage(stage);
            var plain = 0;
            for (var wave = 0; wave < _waves.Count; wave++)
                plain += _waves.BaseSize(stage, wave);
            return steps <= plain ? 1 : plain / (double)steps;
        }

        public BigNumber NormalHp(long stage) => EnemyHp(stage) * NormalShare(stage);

        public BigNumber NormalGold(long stage) => EnemyGold(stage) * NormalShare(stage);

        public int WaveAfterKills(long stage, int kills)
        {
            var wave = 0;
            while (wave < _waves.Count && kills >= _waves.Size(stage, wave))
                kills -= _waves.Size(stage, wave++);
            return wave;
        }

        public double WaveFillAfterKills(long stage, int kills)
        {
            var wave = 0;
            while (wave < _waves.Count && kills >= _waves.Size(stage, wave))
                kills -= _waves.Size(stage, wave++);
            return wave < _waves.Count ? kills / (double)_waves.Size(stage, wave) : 1d;
        }

        public double ItemLevelRate { get; private set; } = 1;

        public int ItemLevelAt(long stage) => (int)System.Math.Min(int.MaxValue, (System.Math.Max(1, stage) - 1) / BiomeLength + 1);

        public BigNumber EnemyHp(long stage)
        {
            var walls = WallStagesUpTo(stage) - WallStagesUpTo(1);
            var log10 = System.Math.Log10(_enemyHp.Base) + walls * System.Math.Log10(_wallRate) + (ItemLevelAt(stage) - 1) * System.Math.Log10(ItemLevelRate);
            if (_hpSteps.Length == 0)
                return ExponentialCurve.FromLog10(log10 + (stage - 1 - walls) * System.Math.Log10(_enemyHp.Rate));
            for (var i = 0; i < _hpSteps.Length; i++)
            {
                var low = _hpSteps[i].From;
                var high = System.Math.Min(stage, i + 1 < _hpSteps.Length ? _hpSteps[i + 1].From : long.MaxValue);
                if (high <= low)
                    break;
                var normal = _wallsOnTop ? high - low : high - low - (WallStagesUpTo(high) - WallStagesUpTo(low));
                log10 += normal * _hpSteps[i].Log10Rate;
            }
            return ExponentialCurve.FromLog10(log10);
        }

        public double BossTimer(double bonusSeconds) => System.Math.Min(BossTimerMaxSeconds, System.Math.Max(1, BossTimerSeconds + bonusSeconds));

        public double BossHpMultiplier => _bossHpMultiplier;

        public BigNumber BossHp(long stage) => EnemyHp(stage) * _bossHpMultiplier;

        public BigNumber EnemyGold(long stage)
        {
            if (_goldSteps.Length == 0)
                return _gold.At(stage);
            var log10 = System.Math.Log10(_gold.Base);
            for (var i = 0; i < _goldSteps.Length; i++)
            {
                var low = _goldSteps[i].From;
                var high = System.Math.Min(stage, i + 1 < _goldSteps.Length ? _goldSteps[i + 1].From : long.MaxValue);
                if (high <= low)
                    break;
                log10 += (high - low) * _goldSteps[i].Log10Rate;
            }
            return ExponentialCurve.FromLog10(log10);
        }

        public BigNumber BossGold(long stage) => EnemyGold(stage) * _bossGoldMultiplier;

        public bool IsInSoftWall(long stage)
        {
            if (_wallZoneLength == 0)
                return false;
            var position = stage % _wallPeriod;
            return position == 0 || position > _wallPeriod - _wallZoneLength;
        }

        public string Biome(long stage)
        {
            var block = (System.Math.Max(1, stage) - 1) / BiomeLength;
            var blockStart = block * BiomeLength + 1;
            return _allBiomes[OpenIndex(blockStart, (int)(block % OpenCount(blockStart)))];
        }

        public long OpensAt(int poolIndex) => _opensAt[poolIndex];

        public bool IsOpen(int poolIndex, long stage) => stage >= _opensAt[poolIndex];

        public int OpenCount(long stage)
        {
            var count = 0;
            for (var i = 0; i < _opensAt.Length; i++)
            {
                if (stage >= _opensAt[i])
                    count++;
            }
            return System.Math.Max(1, count);
        }

        public bool RotatesAt(long stage) => RotationMinutes > 0 && OpenCount(stage) > 2;

        private int OpenIndex(long stage, int nth)
        {
            for (var i = 0; i < _opensAt.Length; i++)
            {
                if (stage < _opensAt[i])
                    continue;
                if (nth-- == 0)
                    return i;
            }
            return 0;
        }

        public int BossSlot(long stage) => (int)(((stage - 1) % BiomeLength + 1) / BossSlotStages);

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
