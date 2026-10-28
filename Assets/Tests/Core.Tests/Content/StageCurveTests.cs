using Game.Core.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class StageCurveTests
    {
        private const string Stepped = @"{
  ""version"": 1,
  ""waves"": { ""count"": 4, ""firstEnemies"": 1, ""enemiesPerWave"": 1, ""stagesPerExtraEnemy"": 10, ""maxEnemiesPerWave"": 8 },
  ""enemyHp"": { ""curve"": ""exp"", ""base"": 10, ""rate"": 1.1, ""steps"": [{ ""from"": 11, ""rate"": 1.01 }] },
  ""softWall"": { ""period"": 100, ""zoneLength"": 10, ""extra"": 2 },
  ""boss"": { ""hpMultiplier"": 6, ""timerSeconds"": 30, ""timerMaxSeconds"": 90, ""goldMultiplier"": 5, ""retryAfterKills"": 10 },
  ""bossHint"": { ""afterFails"": 3, ""roundPercent"": 5, ""maxPercent"": 1000 },
  ""gold"": { ""curve"": ""exp"", ""base"": 4, ""rate"": 1.07, ""steps"": [{ ""from"": 21, ""rate"": 1.02 }] },
  ""biomeLength"": 50,
  ""biomes"": [""green_hills""]
}";

        private static StageTable Table() => StageTable.Load(Stepped);

        [Test]
        public void HpRateChangesAtItsStep()
        {
            var table = Table();

            Assert.That(table.EnemyHp(11).ToDouble(), Is.EqualTo(10 * System.Math.Pow(1.1, 10)).Within(1e-6));
            Assert.That(table.EnemyHp(31).ToDouble(), Is.EqualTo(10 * System.Math.Pow(1.1, 10) * System.Math.Pow(1.01, 20)).Within(1e-6));
        }

        [Test]
        public void WallsMultiplyOnTopOfTheCurrentStep()
        {
            var table = Table();
            var before = table.EnemyHp(90);

            Assert.That((table.EnemyHp(91) / before).ToDouble(), Is.EqualTo(1.01 * 2).Within(1e-9));
            Assert.That((table.EnemyHp(101) / table.EnemyHp(100)).ToDouble(), Is.EqualTo(1.01).Within(1e-9));
        }

        [Test]
        public void GoldRateChangesAtItsStep()
        {
            var table = Table();

            Assert.That(table.EnemyGold(21).ToDouble(), Is.EqualTo(4 * System.Math.Pow(1.07, 20)).Within(1e-6));
            Assert.That(table.EnemyGold(31).ToDouble(), Is.EqualTo(4 * System.Math.Pow(1.07, 20) * System.Math.Pow(1.02, 10)).Within(1e-6));
        }

        [Test]
        public void StepsMustGoUpByStage()
        {
            var broken = Stepped.Replace("\"from\": 11", "\"from\": 1");

            Assert.Throws<ContentException>(() => StageTable.Load(broken));
        }

        [Test]
        public void RealCurveStaysAsBefore()
        {
            var real = TestWorld.Real.Stages;

            Assert.That((real.EnemyHp(51) / real.EnemyHp(50)).ToDouble(), Is.EqualTo(1.08).Within(1e-9));
            Assert.That((real.EnemyHp(92) / real.EnemyHp(91)).ToDouble(), Is.EqualTo(1.2).Within(1e-9));
        }
    }
}
