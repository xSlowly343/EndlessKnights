using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WhispererCurseTests
    {
        private static (GameLoop Loop, FakeInputSource Input) Whisperer(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Whisperer }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static double Ticks(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Tick();
            return loop.State.Resource;
        }

        [Test]
        public void BlightGrowsByItselfAndFromKeys()
        {
            var (loop, input) = Whisperer();

            Assert.That(Ticks(loop, 10), Is.EqualTo(1).Within(1e-9));

            input.PressKeys(5);
            loop.Tick();
            Assert.That(loop.State.Resource, Is.EqualTo(1 + 0.1 + 2.5).Within(1e-9));
        }

        [Test]
        public void MouseGivesNoBlight()
        {
            var (loop, input) = Whisperer();

            input.MoveMouse(100000);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(0.1).Within(1e-9));
        }

    }
}
