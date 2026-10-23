using Game.Core.Sim;
using Game.Core.Sim.Headless;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SimulationTests
    {
        [Test]
        public void BotTypesAtTheRequestedRate()
        {
            var bot = new BotInput(new BotProfile(keysPerSecond: 6, clicksPerSecond: 2, mousePxPerSecond: 500), seed: 3);
            var keys = 0;
            var clicks = 0;
            var mouse = 0.0;

            for (var tick = 0; tick < 6000; tick++)
            {
                var snapshot = bot.Drain();
                keys += snapshot.KeyDowns;
                clicks += snapshot.LeftClicks;
                mouse += snapshot.MouseDistancePx;
            }

            Assert.That(keys / 600.0, Is.EqualTo(6).Within(0.1));
            Assert.That(clicks / 600.0, Is.EqualTo(2).Within(0.05));
            Assert.That(mouse / 600.0, Is.EqualTo(500).Within(10));
        }

        [Test]
        public void HourOfTypingClearsManyStagesAndEndsAtTheWall()
        {
            var result = new Simulation(TestWorld.Data, new BotProfile(6)).Run(3600);

            Assert.That(result.Seconds, Is.EqualTo(3600).Within(0.01));
            Assert.That(result.HighestStage, Is.GreaterThan(20));
            Assert.That(result.Stages.Count, Is.EqualTo(result.HighestStage - 1));
            Assert.That(result.Stages[0].Stage, Is.EqualTo(1));
            var last = result.Stages[result.Stages.Count - 1];
            Assert.That(last.StageSeconds, Is.GreaterThan(result.Stages[0].StageSeconds * 10));
            Assert.That(last.SecondsToBoss + last.BossSeconds, Is.EqualTo(last.StageSeconds).Within(0.01));
        }

        [Test]
        public void FasterTypingGoesFurther()
        {
            var slow = new Simulation(TestWorld.Data, new BotProfile(3)).Run(1800);
            var fast = new Simulation(TestWorld.Data, new BotProfile(8)).Run(1800);

            Assert.That(fast.HighestStage, Is.GreaterThan(slow.HighestStage));
            Assert.That(fast.GoldEarned, Is.GreaterThan(slow.GoldEarned));
        }

        [Test]
        public void TypingAboveTheCapGivesNoMoreThanTheCap()
        {
            var capped = new Simulation(TestWorld.Data, new BotProfile(15)).Run(1800);
            var macro = new Simulation(TestWorld.Data, new BotProfile(40)).Run(1800);

            Assert.That(macro.HighestStage, Is.LessThanOrEqualTo(capped.HighestStage + 1));
        }

        [Test]
        public void SameSeedGivesSameRun()
        {
            var first = new Simulation(TestWorld.Data, new BotProfile(6, 1), HeroClass.Metalhead, seed: 9).Run(1200);
            var second = new Simulation(TestWorld.Data, new BotProfile(6, 1), HeroClass.Metalhead, seed: 9).Run(1200);

            Assert.That(second.HighestStage, Is.EqualTo(first.HighestStage));
            Assert.That(second.GoldEarned, Is.EqualTo(first.GoldEarned));
        }

        [Test]
        public void CanStopWhenStuckAtABoss()
        {
            var result = new Simulation(TestWorld.Data, new BotProfile(2)).Run(24 * 3600, stopAfterBossFailsInARow: 3);

            Assert.That(result.Stuck, Is.True);
            Assert.That(result.Seconds, Is.LessThan(24 * 3600));
        }

        [Test]
        public void CsvRowMatchesHeader()
        {
            var result = new Simulation(TestWorld.Data, new BotProfile(6)).Run(60);

            var header = StageRecord.CsvHeader.Split(',');
            var row = result.Stages[0].ToCsv().Split(',');

            Assert.That(row.Length, Is.EqualTo(header.Length));
            Assert.That(row[0], Is.EqualTo("1"));
            Assert.That(result.Stages[0].ToCsv(), Does.Not.Contain(" "));
        }
    }
}
