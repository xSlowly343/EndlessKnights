using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SandwalkerPathTests
    {
        private static ClassResource Path => TestWorld.Data.Combat.Resources.For("sandwalker");

        private static (GameLoop Loop, FakeInputSource Input) Sandwalker()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Sandwalker }, new StatSheet());
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            return (loop, input);
        }

        [Test]
        public void PathComesALittleFromEverything()
        {
            var (loop, input) = Sandwalker();

            for (var i = 0; i < 2; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }
            Assert.That(loop.State.Resource, Is.EqualTo(2 * Path.PerKey).Within(1e-9));

            for (var i = 0; i < 5; i++)
            {
                input.MoveMouse((float)Path.MousePxPerPoint / 5);
                loop.Tick();
            }
            Assert.That(loop.State.Resource, Is.EqualTo(2 * Path.PerKey + 1).Within(0.05));
        }

        [Test]
        public void EveryClickGivesPath()
        {
            var (loop, input) = Sandwalker();

            input.ClickMiddle();
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Path.PerClick + Path.PerKey).Within(1e-9));
        }

        [Test]
        public void FathersScimitarIsTheNomadsStartingSkill()
        {
            var gem = TestWorld.Real.Gems["sandwalker_hit_1"];

            Assert.That(TestWorld.Real.SkillBook.StartingGems("sandwalker"), Has.Some.Property("Id").EqualTo("sandwalker_hit_1"));
            Assert.That(gem.Skill!.Weapon, Is.EqualTo("scimitar"));
            Assert.That(gem.Skill.ResourceCost, Is.EqualTo(10));
        }
    }
}
