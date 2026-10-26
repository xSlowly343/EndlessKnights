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
        private static ClassRules Rules => TestWorld.Data.Combat.Classes;

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

        private static GameEvent LastHit(GameLoop loop)
        {
            for (var i = loop.Events.Count - 1; i >= 0; i--)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage)
                    return loop.Events[i];
            }
            throw new AssertionException("no damage events");
        }

        [Test]
        public void TypistComboGrowsWithEachKey()
        {
            var (loop, input) = Hero(HeroClass.Ronin);

            Keys(loop, input, 7);

            Assert.That(loop.State.Combo, Is.EqualTo(7));
        }

        [Test]
        public void EveryNthKeyIsAComboStrike()
        {
            var (loop, input) = Hero(HeroClass.Ronin);

            Keys(loop, input, Rules.ComboStrikeEvery - 1);
            var normal = LastHit(loop);
            Keys(loop, input, 1);
            var strike = LastHit(loop);

            Assert.That((normal.Flags & HitFlags.ComboStrike) == 0, Is.True);
            Assert.That((strike.Flags & HitFlags.ComboStrike) != 0, Is.True);
            var normalDamage = normal.Amount.ToDouble();
            Assert.That(strike.Amount.ToDouble() / normalDamage, Is.EqualTo(Rules.ComboStrikeMultiplier).Within(0.05));
        }

        [Test]
        public void ComboSurvivesShortPausesAndBreaksOnLongOnes()
        {
            var (loop, input) = Hero(HeroClass.Ronin);
            var timeoutTicks = (int)System.Math.Round(Rules.ComboTimeoutSeconds * GameLoop.TicksPerSecond);
            Keys(loop, input, 10);

            for (var i = 0; i < timeoutTicks - 1; i++)
                loop.Tick();
            Assert.That(loop.State.Combo, Is.EqualTo(10));

            for (var i = 0; i < 2; i++)
                loop.Tick();
            Assert.That(loop.State.Combo, Is.EqualTo(0));
        }

        [Test]
        public void ClicksDoNotKeepTheComboAlive()
        {
            var (loop, input) = Hero(HeroClass.Ronin);
            var timeoutTicks = (int)System.Math.Round(Rules.ComboTimeoutSeconds * GameLoop.TicksPerSecond);
            Keys(loop, input, 5);

            for (var i = 0; i < timeoutTicks + 2; i++)
            {
                input.ClickLeft();
                loop.Tick();
            }

            Assert.That(loop.State.Combo, Is.EqualTo(0));
        }

        [Test]
        public void BladeDanceKeepsTheComboAliveWhileTheMouseMoves()
        {
            var (loop, input) = Hero(HeroClass.Ronin);
            var timeoutTicks = (int)System.Math.Round(Rules.ComboTimeoutSeconds * GameLoop.TicksPerSecond);
            Keys(loop, input, 5);

            for (var i = 0; i < timeoutTicks * 3; i++)
            {
                input.MoveMouse(500);
                loop.Tick();
            }

            Assert.That(loop.State.Combo, Is.EqualTo(5));
        }

        [Test]
        public void ThinCutAlwaysCrits()
        {
            var (loop, _) = Hero(HeroClass.Ronin);
            var gem = TestWorld.Real.Gems["thin_cut"];
            loop.State.Resource = 100;
            TestWorld.ArmFor(loop, gem);
            loop.Events.Clear();

            Assert.That(loop.Skills.TryCast(loop.Skills.Equip(gem)), Is.EqualTo(CastResult.Cast));

            Assert.That(LastHit(loop).Flags & HitFlags.Crit, Is.EqualTo(HitFlags.Crit));
            Assert.That(TestWorld.Real.SkillBook.StartingGems("ronin"), Has.Some.Property("Id").EqualTo("thin_cut"));
        }

        [Test]
        public void EveryComboStrikeGivesOneWhirl()
        {
            var (loop, input) = Hero(HeroClass.Ronin);

            Keys(loop, input, Rules.ComboStrikeEvery);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Rules.WhirlResource).Within(1e-9));
        }

        [Test]
        public void MetalheadHasNoCombo()
        {
            var (loop, input) = Hero(HeroClass.Metalhead);

            Keys(loop, input, Rules.ComboStrikeEvery);

            Assert.That(loop.State.Combo, Is.EqualTo(0));
            Assert.That((LastHit(loop).Flags & HitFlags.ComboStrike) == 0, Is.True);
        }
    }
}
