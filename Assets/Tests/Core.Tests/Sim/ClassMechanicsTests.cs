using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ClassMechanicsTests
    {

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Keys(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        [Test]
        public void DuelistHitsNoLongerBuildTheOldSeries()
        {
            var (loop, input) = Hero(HeroClass.Duelist);

            Keys(loop, input, 60);

            Assert.That(loop.State.Combo, Is.Zero);
            Assert.That(loop.State.Resource, Is.EqualTo(60 * TestWorld.Data.Combat.Resources.For("duelist").PerKey).Within(1e-6));
        }
    }
}
