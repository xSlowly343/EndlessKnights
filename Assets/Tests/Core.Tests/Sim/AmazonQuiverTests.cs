using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AmazonQuiverTests
    {
        private const double Arrow = 5;
        private static ClassResource Quiver => TestWorld.Data.Combat.Resources.For("amazon");
        private static GemDef RoadArrow => TestWorld.Real.Gems["arrow_from_the_road"];
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;

        private static (GameLoop Loop, FakeInputSource Input) Amazon(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Amazon }, hero);
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
            var (loop, _) = Amazon();

            Wait(loop, 5);

            Assert.That(loop.State.Resource, Is.EqualTo(Arrow).Within(1e-9));
        }

        [Test]
        public void EightHundredPixelsOfMouseBringOneArrow()
        {
            var (loop, input) = Amazon();

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
            var (loop, input) = Amazon();

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Quiver.PerSecond * GameLoop.TickSeconds).Within(1e-9));
        }

        [Test]
        public void RoadArrowCostsOneArrowAndArrowDamageMakesItStronger()
        {
            var (loop, _) = Amazon(new Modifier(CombatStats.ArrowDamage, ModifierKind.Increased, 0.5));
            loop.State.Resource = Arrow;
            TestWorld.ArmFor(loop, RoadArrow);
            var hp = loop.State.EnemyHp;

            Assert.That(loop.Skills.TryCast(loop.Skills.Equip(RoadArrow)), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.State.Resource, Is.EqualTo(0).Within(1e-9));
            var dealt = (hp - loop.State.EnemyHp).ToDouble();
            var hit = KeyDamage * 3 * 1.5;
            Assert.That(dealt, Is.EqualTo(hit).Within(1e-6).Or.EqualTo(hit * TestWorld.Data.Combat.CritMultiplier).Within(1e-6));
            Assert.That(TestWorld.Real.SkillBook.StartingGems("amazon"), Has.Some.Property("Id").EqualTo("arrow_from_the_road"));
        }

        [Test]
        public void FalconGazeIsAnArrowThatAlwaysCrits()
        {
            var strike = (StrikeEffect)TestWorld.Real.Gems["falcon_gaze"].Skill!.Effect;

            Assert.That(strike.CritChance, Is.EqualTo(1));
            Assert.That(strike.Arrow, Is.True);
        }

        [Test]
        public void PantherHitsTwiceASecondAndFasterWhileTheMouseMoves()
        {
            var (still, _) = Amazon();
            var (running, input) = Amazon();
            var gem = TestWorld.Real.Gems["panther_at_side"];
            var panther = (SummonEffect)gem.Skill!.Effect;
            still.Skills.Minions.Place(panther, gem.Tags);
            running.Skills.Minions.Place(panther, gem.Tags);

            Wait(still, 10);
            for (var i = 0; i < 10 * GameLoop.TicksPerSecond; i++)
            {
                input.MoveMouse(1);
                running.Tick();
            }

            Assert.That(still.Skills.Minions.BeastHits, Is.EqualTo(20));
            Assert.That(running.Skills.Minions.BeastHits, Is.EqualTo((long)(20 * TestWorld.Data.Combat.Beasts.MouseSpeed)));
            Assert.That(gem.Locked, Is.False);
        }
    }
}
