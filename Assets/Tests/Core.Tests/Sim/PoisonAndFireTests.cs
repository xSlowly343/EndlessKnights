using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PoisonAndFireTests
    {
        private static DotRules Rules => TestWorld.Data.Combat.Dots;
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;
        private static GemDef Vial => TestWorld.Real.Gems["alchemist_hit_1"];
        private static GemDef Bottle => TestWorld.Real.Gems["dowser_hit_1"];

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        private static int Equip(GameLoop loop, GemDef gem)
        {
            TestWorld.ArmFor(loop, gem);
            return loop.Skills.Equip(gem);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static double Dealt(GameLoop loop, BigNumber hp) => (hp - loop.State.EnemyHp).ToDouble();

        [Test]
        public void EveryTenthKeyHitOfAVenomistAddsAPoisonStack()
        {
            var (venomist, venomistKeys) = Hero(HeroClass.Alchemist);
            var (huscarl, huscarlKeys) = Hero(HeroClass.Runedancer);

            for (var tick = 0; tick < 10; tick++)
            {
                venomistKeys.PressKeys(3);
                huscarlKeys.PressKeys(3);
                venomist.Tick();
                huscarl.Tick();
            }

            Assert.That(Rules.VapourKeyHits, Is.EqualTo(10));
            Assert.That(venomist.State.KeyHits, Is.GreaterThan(10));
            Assert.That(venomist.State.PoisonStacks, Is.EqualTo(venomist.State.KeyHits / Rules.VapourKeyHits).Within(0.1), "a stack of poisonous vapours per ten counted key hits");
            Assert.That(huscarl.State.PoisonStacks, Is.Zero, "only the Venomist breathes vapours");
        }

        [Test]
        public void PoisonousVapoursStopAtThePoisonCap()
        {
            var (loop, input) = Hero(HeroClass.Alchemist);

            for (var tick = 0; tick < 3000; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
            }

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(loop.Skills.Dots.PoisonMax).Within(1e-9));
        }

        [Test]
        public void PoisonTicksOncePerSecondEvenWithoutInput()
        {
            var (loop, _) = Hero(HeroClass.Alchemist);
            loop.State.PoisonStacks = 10;
            var hp = loop.State.EnemyHp;

            Wait(loop, 3);

            Assert.That(Dealt(loop, hp), Is.EqualTo(KeyDamage * 10 * Rules.PoisonHitsPerStack * 3).Within(1e-6));
        }

        [Test]
        public void PoisonStacksStopAtTheLimit()
        {
            var (loop, _) = Hero(HeroClass.Alchemist);
            loop.State.PoisonStacks = Rules.PoisonMax - 1;

            loop.Skills.Dots.Apply(new AddStacksEffect(DotKind.Poison, 3, 0, 0));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(Rules.PoisonMax));
        }

        [Test]
        public void PoisonStacksStatRaisesTheLimit()
        {
            var (loop, _) = Hero(HeroClass.Alchemist, new Modifier(CombatStats.PoisonStacks, ModifierKind.Flat, 2));
            loop.State.PoisonStacks = Rules.PoisonMax;

            loop.Skills.Dots.Apply(new AddStacksEffect(DotKind.Poison, 3, 0, 0));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(Rules.PoisonMax + 2));
        }

        [Test]
        public void StirringWithTheMouseAddsStacksToTheNextVial()
        {
            var (loop, input) = Hero(HeroClass.Alchemist);
            for (var i = 0; i < Rules.StirMax + 3; i++)
            {
                input.MoveMouse((float)Rules.StirPxPerTurn);
                loop.Tick();
            }

            loop.Skills.Dots.Apply(new AddStacksEffect(DotKind.Poison, 3, 0, 0));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(3 + Rules.StirMax));
            Assert.That(loop.Skills.Dots.Stir, Is.EqualTo(0));
        }

        [Test]
        public void StacksLeaveWithTheEnemy()
        {
            var (loop, _) = Hero(HeroClass.Alchemist);
            loop.State.PoisonStacks = 10;
            loop.State.BurnStacks = 5;

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(0));
            Assert.That(loop.State.BurnStacks, Is.EqualTo(0));
        }

        [Test]
        public void DotDamageAndDotTaggedDamageMakeTicksStronger()
        {
            var (loop, _) = Hero(HeroClass.Alchemist,
                new Modifier(CombatStats.DotDamage, ModifierKind.Increased, 0.5),
                new Modifier(CombatStats.Damage, ModifierKind.More, 1, StatTags.DoT));
            loop.State.PoisonStacks = 10;
            var hp = loop.State.EnemyHp;

            Wait(loop, 1);

            Assert.That(Dealt(loop, hp), Is.EqualTo(KeyDamage * 10 * Rules.PoisonHitsPerStack * 1.5 * 2).Within(1e-6));
        }




    }
}
