using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BotPrestigeTests
    {
        private static (GameLoop Loop, PlayerBot Bot) Bot()
        {
            var state = new GameState(4) { Class = HeroClass.Viking, Stage = 60, HighestStage = 60, RecordStage = 60 };
            var loop = TestWorld.Loop(state: state);
            loop.State.RunGoldEarned = BigNumber.FromDouble(1e9);
            return (loop, new PlayerBot(TestWorld.Data, loop, 20));
        }

        [Test]
        public void ABossNotKilledInTimeLeadsToPrestige()
        {
            var (loop, bot) = Bot();
            loop.State.BossFailsInARow = 1;

            bot.Act(0.1);

            Assert.That(loop.State.Prestiges, Is.EqualTo(1));
        }

        [Test]
        public void MoreThanAMinuteFromMobsToTheBossLeadsToPrestige()
        {
            var (loop, bot) = Bot();

            bot.Act(30);
            Assert.That(loop.State.Prestiges, Is.Zero);
            bot.Act(31);
            Assert.That(loop.State.Prestiges, Is.Zero);
            bot.Act(31);

            Assert.That(loop.State.Prestiges, Is.EqualTo(1));
        }
    }
}
