using Game.Core.Content;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class StageTableTests
    {
        private const string Valid = @"{
  ""version"": 1,
  ""enemiesPerStage"": 10,
  ""enemyHp"": { ""curve"": ""exp"", ""base"": 20, ""rate"": 1.08 },
  ""softWall"": { ""period"": 100, ""zoneLength"": 10, ""rate"": 1.2 },
  ""boss"": { ""hpMultiplier"": 6, ""timerSeconds"": 30, ""timerMaxSeconds"": 90, ""goldMultiplier"": 5, ""retryAfterKills"": 10 },
  ""bossHint"": { ""afterFails"": 3, ""roundPercent"": 5, ""maxPercent"": 1000 },
  ""party"": { ""hpPerExtraPlayer"": 1.0 },
  ""gold"": { ""curve"": ""exp"", ""base"": 4, ""rate"": 1.07 },
  ""biomeLength"": 50,
  ""biomes"": [""green_hills"", ""sand_ruins""]
}";

        private static StageTable Table() => StageTable.Load(Valid);

        [Test]
        public void RealDataFileLoads()
        {
            var table = StageTable.Load(TestData.ReadGameDataFile(StageTable.FileName));

            Assert.That(table.EnemiesPerStage, Is.EqualTo(10));
            Assert.That(table.BossTimerSeconds, Is.GreaterThan(0));
            Assert.That(table.Biome(1), Is.EqualTo("green_hills"));
        }

        [Test]
        public void RealEnemiesHaveHpFromClassResources()
        {
            var table = StageTable.Load(TestData.ReadGameDataFile(StageTable.FileName));

            Assert.That(table.EnemyHp(1).ToDouble(), Is.EqualTo(120).Within(1e-6));
            Assert.That(table.BossHp(1).ToDouble(), Is.EqualTo(840).Within(1e-6));
        }

        [Test]
        public void PartyEnemyHpGrowsLinearlyWithPlayers()
        {
            var table = Table();

            Assert.That(table.PartyHpMultiplier(1), Is.EqualTo(1));
            Assert.That(table.PartyHpMultiplier(2), Is.EqualTo(2));
            Assert.That(table.PartyHpMultiplier(4), Is.EqualTo(4));
            Assert.That(table.PartyHpMultiplier(0), Is.EqualTo(1));
        }

        [Test]
        public void RealDataCurvesGrowEveryStage()
        {
            var table = StageTable.Load(TestData.ReadGameDataFile(StageTable.FileName));

            for (var stage = 2; stage <= 1000; stage++)
            {
                Assert.That(table.EnemyHp(stage) > table.EnemyHp(stage - 1), $"HP must grow at stage {stage}");
                Assert.That(table.EnemyGold(stage) > table.EnemyGold(stage - 1), $"gold must grow at stage {stage}");
                Assert.That(table.BossHp(stage) > table.EnemyHp(stage), $"boss must be tougher at stage {stage}");
            }
        }

        [Test]
        public void FirstStageMatchesBase()
        {
            Assert.That(Table().EnemyHp(1).ToDouble(), Is.EqualTo(20).Within(1e-9));
            Assert.That(Table().EnemyGold(1).ToDouble(), Is.EqualTo(4).Within(1e-9));
            Assert.That(Table().BossHp(1).ToDouble(), Is.EqualTo(120).Within(1e-9));
        }

        [Test]
        public void NormalStagesGrowByRate()
        {
            var table = Table();

            Assert.That((table.EnemyHp(51) / table.EnemyHp(50)).ToDouble(), Is.EqualTo(1.08).Within(1e-9));
        }

        [Test]
        public void SoftWallZoneGrowsFaster()
        {
            var table = Table();

            Assert.That((table.EnemyHp(95) / table.EnemyHp(94)).ToDouble(), Is.EqualTo(1.2).Within(1e-9));
            Assert.That((table.EnemyHp(101) / table.EnemyHp(100)).ToDouble(), Is.EqualTo(1.08).Within(1e-9));
            Assert.That(table.IsInSoftWall(91), Is.True);
            Assert.That(table.IsInSoftWall(100), Is.True);
            Assert.That(table.IsInSoftWall(90), Is.False);
            Assert.That(table.IsInSoftWall(101), Is.False);
        }

        [Test]
        public void HpAtStageAThousandMatchesClosedForm()
        {
            var expectedLog = System.Math.Log10(20) + 899 * System.Math.Log10(1.08) + 100 * System.Math.Log10(1.2);

            Assert.That(Table().EnemyHp(1000).Log10(), Is.EqualTo(expectedLog).Within(1e-9));
        }

        [Test]
        public void HugeStagesStayFinite()
        {
            var hp = Table().EnemyHp(100000);

            Assert.That(hp.Log10(), Is.GreaterThan(3000));
        }

        [Test]
        public void BiomesCycleEveryBiomeLength()
        {
            var table = Table();

            Assert.That(table.Biome(50), Is.EqualTo("green_hills"));
            Assert.That(table.Biome(51), Is.EqualTo("sand_ruins"));
            Assert.That(table.Biome(101), Is.EqualTo("green_hills"));
            Assert.That(table.IsBiomeGuardian(50), Is.True);
            Assert.That(table.IsBiomeGuardian(49), Is.False);
        }

        [TestCase("\"version\": 1", "\"version\": 2", "version: version 2 is not supported")]
        [TestCase("\"rate\": 1.08", "\"rate\": 0.9", "enemyHp.rate: 0.9 is outside the allowed range")]
        [TestCase("\"zoneLength\": 10", "\"zoneLength\": 100", "softWall.zoneLength: must be shorter than period (100)")]
        [TestCase("\"rate\": 1.2", "\"rate\": 1.01", "softWall.rate: must not be lower than enemyHp.rate")]
        [TestCase("\"curve\": \"exp\", \"base\": 4", "\"curve\": \"cubic\", \"base\": 4", "gold.curve: unsupported curve 'cubic'")]
        [TestCase("[\"green_hills\", \"sand_ruins\"]", "[]", "biomes: at least one biome is required")]
        [TestCase("\"timerSeconds\": 30", "\"timerSeconds\": \"30\"", "boss.timerSeconds: expected a number, got a string")]
        public void BrokenFieldsAreReportedByPath(string original, string broken, string message)
        {
            var json = Valid.Replace(original, broken);

            var error = Assert.Throws<ContentException>(() => StageTable.Load(json))!;

            Assert.That(error.Message, Does.Contain("stages.json: " + message));
        }
    }
}
