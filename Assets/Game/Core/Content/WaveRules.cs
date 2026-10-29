namespace Game.Core.Content
{
    public sealed class WaveRules
    {
        private readonly int _firstEnemies;
        private readonly int _enemiesPerWave;
        private readonly int _stagesPerExtraEnemy;
        private readonly int _maxEnemies;

        private WaveRules(int count, int firstEnemies, int enemiesPerWave, int stagesPerExtraEnemy, int maxEnemies)
        {
            Count = count;
            _firstEnemies = firstEnemies;
            _enemiesPerWave = enemiesPerWave;
            _stagesPerExtraEnemy = stagesPerExtraEnemy;
            _maxEnemies = maxEnemies;
        }

        public int Count { get; }
        public int FirstEnemies => _firstEnemies;
        public int EnemiesPerWave => _enemiesPerWave;
        public int StagesPerExtraEnemy => _stagesPerExtraEnemy;
        public int MaxEnemies => _maxEnemies;

        public static WaveRules Read(ContentNode node) =>
            new WaveRules(
                node.Field("count").AsInt(1, 10),
                node.Field("firstEnemies").AsInt(1, 100),
                node.Field("enemiesPerWave").AsInt(0, 100),
                node.Field("stagesPerExtraEnemy").AsInt(1, 100000),
                node.Field("maxEnemiesPerWave").AsInt(1, 100));

        public int Size(long stage, int wave)
        {
            var size = _firstEnemies + wave * _enemiesPerWave + (int)System.Math.Min(_maxEnemies, (stage - 1) / _stagesPerExtraEnemy);
            return System.Math.Min(_maxEnemies, size);
        }
    }
}
