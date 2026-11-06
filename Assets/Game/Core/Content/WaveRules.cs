using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class WaveRules
    {
        private readonly int _firstEnemies;
        private readonly int _enemiesPerWave;
        private readonly int _stagesPerExtraEnemy;
        private readonly int _maxEnemies;
        private readonly long[] _stepFrom;
        private readonly int[] _stepMax;

        private WaveRules(int count, int firstEnemies, int enemiesPerWave, int stagesPerExtraEnemy, int maxEnemies, long[] stepFrom, int[] stepMax)
        {
            Count = count;
            _firstEnemies = firstEnemies;
            _enemiesPerWave = enemiesPerWave;
            _stagesPerExtraEnemy = stagesPerExtraEnemy;
            _maxEnemies = maxEnemies;
            _stepFrom = stepFrom;
            _stepMax = stepMax;
        }

        public int Count { get; }
        public int FirstEnemies => _firstEnemies;
        public int EnemiesPerWave => _enemiesPerWave;
        public int StagesPerExtraEnemy => _stagesPerExtraEnemy;
        public int MaxEnemies => _maxEnemies;
        public System.Collections.Generic.IReadOnlyList<long> StepFrom => _stepFrom;
        public System.Collections.Generic.IReadOnlyList<int> StepMax => _stepMax;

        public int MaxEnemiesAt(long stage)
        {
            var max = _maxEnemies;
            for (var i = 0; i < _stepFrom.Length; i++)
            {
                if (stage >= _stepFrom[i])
                    max = _stepMax[i];
            }
            return max;
        }

        public static WaveRules Read(ContentNode node)
        {
            var max = node.Field("maxEnemiesPerWave").AsInt(1, 1000);
            var from = new List<long>();
            var maxes = new List<int>();
            if (node.HasField("maxSteps"))
            {
                foreach (var step in node.Field("maxSteps").Items())
                {
                    var stage = step.Field("from").AsLong(2);
                    var stepMax = step.Field("max").AsInt(1, 1000);
                    var previousStage = from.Count > 0 ? from[from.Count - 1] : 1;
                    var previousMax = maxes.Count > 0 ? maxes[maxes.Count - 1] : max;
                    if (stage <= previousStage)
                        throw step.Field("from").Error("wave steps must go up in stage");
                    if (stepMax < previousMax)
                        throw step.Field("max").Error("enemies per wave must not drop");
                    from.Add(stage);
                    maxes.Add(stepMax);
                }
            }
            return new WaveRules(
                node.Field("count").AsInt(1, 10),
                node.Field("firstEnemies").AsInt(1, 100),
                node.Field("enemiesPerWave").AsInt(0, 100),
                node.Field("stagesPerExtraEnemy").AsInt(1, 100000),
                max,
                from.ToArray(),
                maxes.ToArray());
        }

        public int Size(long stage, int wave) => SizeWithMax(stage, wave, MaxEnemiesAt(stage));

        public int BaseSize(long stage, int wave) => SizeWithMax(stage, wave, _maxEnemies);

        private int SizeWithMax(long stage, int wave, int max)
        {
            var size = _firstEnemies + wave * _enemiesPerWave + (int)System.Math.Min(max, (stage - 1) / _stagesPerExtraEnemy);
            return System.Math.Min(max, size);
        }
    }
}
