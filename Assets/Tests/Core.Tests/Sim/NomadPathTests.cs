using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class NomadPathTests
    {
        private static ClassResource Path => TestWorld.Data.Combat.Resources.For("nomad");

        private static (GameLoop Loop, FakeInputSource Input) Nomad()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Nomad }), new StatSheet());
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            return (loop, input);
        }

        [Test]
        public void PathComesALittleFromEverything()
        {
            var (loop, input) = Nomad();

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
            var (loop, input) = Nomad();

            input.ClickMiddle();
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Path.PerClick).Within(1e-9));
        }

        [Test]
        public void FathersScimitarIsTheNomadsStartingSkill()
        {
            var gem = TestWorld.Real.Gems["fathers_scimitar"];

            Assert.That(TestWorld.Real.SkillBook.StartingGems("nomad"), Has.Some.Property("Id").EqualTo("fathers_scimitar"));
            Assert.That(gem.Skill!.Weapon, Is.EqualTo("scimitar"));
            Assert.That(gem.Skill.ResourceCost, Is.EqualTo(10));
        }
    }
}
