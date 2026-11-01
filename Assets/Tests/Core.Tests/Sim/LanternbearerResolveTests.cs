using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LanternbearerResolveTests
    {
        private static ClassResource Resolve => TestWorld.Data.Combat.Resources.For("lanternbearer");

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, new StatSheet());
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            return (loop, input);
        }

        [Test]
        public void ResolveComesFromKeys()
        {
            var (loop, input) = Hero(HeroClass.Lanternbearer);

            for (var i = 0; i < 3; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }

            Assert.That(loop.State.Resource, Is.EqualTo(3 * Resolve.PerKey).Within(1e-9));
        }

        [Test]
        public void ForemansHammerIsTheStartingSkill()
        {
            Assert.That(TestWorld.Real.SkillBook.StartingGems("lanternbearer"), Has.Some.Property("Id").EqualTo("lanternbearer_hit_1"));
            Assert.That(TestWorld.Real.Gems["lanternbearer_hit_1"].Skill!.Weapon, Is.EqualTo("lantern_staff"));
        }
    }
}
