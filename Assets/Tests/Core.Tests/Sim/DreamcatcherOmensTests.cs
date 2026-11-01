using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DreamcatcherOmensTests
    {
        private static ClassResource Omens => TestWorld.Data.Combat.Resources.For("dreamcatcher");

        private static (GameLoop Loop, FakeInputSource Input) Dreamcatcher()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Dreamcatcher }, new StatSheet());
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            return (loop, input);
        }

        private static void Move(GameLoop loop, FakeInputSource input, int ticks, float px)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.MoveMouse(px);
                loop.Tick();
            }
        }

        [Test]
        public void DrowseComesFromHitsAndSteps()
        {
            var (loop, input) = Dreamcatcher();

            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.Resource, Is.EqualTo(Omens.PerKey).Within(0.05));

            Move(loop, input, 6, (float)Omens.MousePxPerPoint / 6);
            Assert.That(loop.State.Resource, Is.EqualTo(Omens.PerKey + 1).Within(0.05));
        }

        [Test]
        public void ReadingWithoutKeysGivesAnOmenEverySecond()
        {
            var (loop, input) = Dreamcatcher();
            var readingTicks = (int)System.Math.Round(Omens.ReadingAfterKeylessSeconds * GameLoop.TicksPerSecond);

            Move(loop, input, readingTicks, 0.001f);
            var before = loop.State.Resource;
            Move(loop, input, 2 * GameLoop.TicksPerSecond, 0.001f);

            Assert.That(loop.State.Resource - before, Is.EqualTo(2 * Omens.ReadingPerSecond).Within(0.11));
        }

        [Test]
        public void NoReadingWhileTheMouseStands()
        {
            var (loop, _) = Dreamcatcher();

            for (var i = 0; i < 10 * GameLoop.TicksPerSecond; i++)
                loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(0));
        }
    }
}
