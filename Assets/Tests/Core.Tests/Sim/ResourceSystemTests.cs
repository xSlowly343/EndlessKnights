using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ResourceSystemTests
    {
        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = heroClass }));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return (loop, input);
        }

        private static void Type(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var i = 0; i < keys; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        private static void Ticks(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Tick();
        }

        [Test]
        public void VikingGainsTwoRagePerKey()
        {
            var (loop, input) = Hero(HeroClass.Viking);

            Type(loop, input, 5);

            Assert.That(loop.State.Resource, Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void VikingRageBurnsTenPerSecondAfterThreeSecondsWithoutKeys()
        {
            var (loop, input) = Hero(HeroClass.Viking);
            Type(loop, input, 25);

            Ticks(loop, 29);
            Assert.That(loop.State.Resource, Is.EqualTo(50).Within(1e-9));

            Ticks(loop, 10);
            Assert.That(loop.State.Resource, Is.EqualTo(40).Within(1e-9));
        }

        [Test]
        public void MovingMouseKeepsVikingRageFromBurning()
        {
            var (loop, input) = Hero(HeroClass.Viking);
            Type(loop, input, 25);

            for (var i = 0; i < 60; i++)
            {
                input.MoveMouse(50);
                loop.Tick();
            }

            Assert.That(loop.State.Resource, Is.EqualTo(50).Within(1e-9));
        }

        [Test]
        public void ResourceNeverGoesBelowZeroOrAboveMax()
        {
            var (loop, input) = Hero(HeroClass.Viking);
            Type(loop, input, 60);
            Assert.That(loop.State.Resource, Is.EqualTo(loop.Resource.Max));

            Ticks(loop, 300);
            Assert.That(loop.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void MetalheadDriveComesFromKeysAndDoesNotBurn()
        {
            var (loop, input) = Hero(HeroClass.Metalhead);

            Type(loop, input, 7);
            input.MoveMouse(5000);
            loop.Tick();
            Ticks(loop, 100);

            Assert.That(loop.State.Resource, Is.EqualTo(7).Within(1e-9));
        }

        [Test]
        public void ClicksAndWheelGiveNoResource()
        {
            var (loop, input) = Hero(HeroClass.Metalhead);

            input.ClickLeft(5);
            input.ClickRight(2);
            input.ScrollWheel(30);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void EveryCharacterHasItsOwnResource()
        {
            var resources = TestWorld.Data.Combat.Resources;

            foreach (var name in HeroClassNames.ByName.Keys)
                Assert.That(resources.For(name), Is.Not.SameAs(resources.Fallback), name);
        }

        [Test]
        public void KeyboardResourceGainRaisesRageFromKeys()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.ResourceGain, ModifierKind.Increased, 0.5, StatTags.Keyboard));
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Viking }), hero);

            Type(loop, input, 10);

            Assert.That(loop.State.Resource, Is.EqualTo(30).Within(1e-9));
        }
    }
}
