using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WindshotQuiverTests
    {
        private const double Arrow = 5;
        private static ClassResource Quiver => TestWorld.Data.Combat.Resources.For("windshot");
        private static GemDef RoadArrow => TestWorld.Real.Gems["windshot_hit_1"];
        private static double StrikeDamage => TestWorld.Data.Combat.StrikeDamage;

        private static (GameLoop Loop, FakeInputSource Input) Windshot(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Windshot }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        [Test]
        public void QuiverGainsAnArrowEveryFiveSecondsByItself()
        {
            var (loop, _) = Windshot();

            Wait(loop, 5);

            Assert.That(loop.State.Resource, Is.EqualTo(Arrow).Within(1e-9));
        }

        [Test]
        public void EightHundredPixelsOfMouseBringOneArrow()
        {
            var (loop, input) = Windshot();

            for (var i = 0; i < 8; i++)
            {
                input.MoveMouse(100);
                loop.Tick();
            }

            var bySelf = Quiver.PerSecond * 0.8;
            Assert.That(loop.State.Resource - bySelf, Is.EqualTo(Arrow).Within(0.05));
        }

        [Test]
        public void KeysBringNoArrows()
        {
            var (loop, input) = Windshot();

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Quiver.PerSecond * GameLoop.TickSeconds).Within(1e-9));
        }
    }
}
