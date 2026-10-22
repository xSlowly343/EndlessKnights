using Game.Core.Sim.Headless;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SoftWallTests
    {
        [Test]
        public void StrongHeroRunsIntoTheHundredStageWall()
        {
            var stages = TestWorld.Data.Stages;

            var result = new Simulation(TestWorld.Data, new BotProfile(6), damageMultiplier: 30).Run(6 * 3600, stopAfterBossFailsInARow: 3);

            Assert.That(result.Stuck, Is.True);
            Assert.That(stages.IsInSoftWall(result.FinalStage), Is.True, $"stuck at {result.FinalStage}");
            Assert.That(result.FinalStage, Is.InRange(91, 100));
        }

        [Test]
        public void StagesSlowDownSharplyInsideTheWall()
        {
            var result = new Simulation(TestWorld.Data, new BotProfile(6), damageMultiplier: 30).Run(6 * 3600, stopAfterBossFailsInARow: 3);

            StageRecord At(long stage)
            {
                foreach (var record in result.Stages)
                {
                    if (record.Stage == stage)
                        return record;
                }
                throw new AssertionException($"stage {stage} not cleared");
            }

            var beforeWall = At(90).StageSeconds / At(89).StageSeconds;
            var insideWall = At(92).StageSeconds / At(91).StageSeconds;
            Assert.That(beforeWall, Is.LessThan(1.12));
            Assert.That(insideWall, Is.GreaterThan(1.15));
        }

        [Test]
        public void GuardianWithKeyboardPenaltyStopsAWeakTypist()
        {
            var result = new Simulation(TestWorld.Data, new BotProfile(3), damageMultiplier: 3).Run(4 * 3600, stopAfterBossFailsInARow: 3);

            Assert.That(result.Stuck, Is.True);
            Assert.That(TestWorld.Data.Stages.IsBiomeGuardian(result.FinalStage), Is.True, $"stuck at {result.FinalStage}");
        }
    }
}
