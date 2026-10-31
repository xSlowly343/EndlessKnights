using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StormweaverStaticTests
    {
        private static ClassResource Static => TestWorld.Data.Combat.Resources.For("stormweaver");
        private static GemDef Spark => TestWorld.Real.Gems["finger_spark"];
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;

        private static (GameLoop Loop, FakeInputSource Input) Stormweaver(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Stormweaver }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        [Test]
        public void StaticComesFromMouseFrictionAndKeys()
        {
            var (loop, input) = Stormweaver();

            for (var i = 0; i < 5; i++)
            {
                input.PressKeys(1);
                input.MoveMouse((float)Static.MousePxPerPoint / 5);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }

            Assert.That(loop.State.Resource, Is.EqualTo(2).Within(0.05));
        }

        [Test]
        public void FullStaticDischargesByItselfAndEmptiesTheBar()
        {
            var (loop, _) = Stormweaver();
            loop.State.Resource = TestWorld.Data.Combat.Resources.Max;
            var hp = loop.State.EnemyHp;

            loop.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(1));
            Assert.That(loop.State.Resource, Is.EqualTo(0).Within(1e-9));
            Assert.That((hp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(KeyDamage * Static.OverloadMultiplier).Within(1e-6));
        }

        [Test]
        public void OverloadMultiplierAndOverloadDisabledChangeTheDischarge()
        {
            var (stronger, _) = Stormweaver(new Modifier(CombatStats.OverloadMultiplier, ModifierKind.Flat, 3));
            var (off, _) = Stormweaver(new Modifier(CombatStats.OverloadDisabled, ModifierKind.Flat, 1));
            stronger.State.Resource = TestWorld.Data.Combat.Resources.Max;
            off.State.Resource = TestWorld.Data.Combat.Resources.Max;
            var hp = stronger.State.EnemyHp;

            stronger.Tick();
            off.Tick();

            Assert.That((hp - stronger.State.EnemyHp).ToDouble(), Is.EqualTo(KeyDamage * (Static.OverloadMultiplier + 3)).Within(1e-6));
            Assert.That(off.Overload.Discharges, Is.EqualTo(0));
            Assert.That(off.State.Resource, Is.EqualTo(TestWorld.Data.Combat.Resources.Max).Within(1e-9));
        }

        [Test]
        public void OtherCharactersNeverOverload()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Metalhead }, new StatSheet());
            loop.State.Resource = TestWorld.Data.Combat.Resources.Max;

            loop.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(0));
        }

        [Test]
        public void FingerSparkHitsTheEnemyAndTwoMoreInTheQueue()
        {
            var (loop, _) = Stormweaver();
            loop.State.Resource = 50;
            TestWorld.ArmFor(loop, Spark);
            var hp = loop.State.EnemyHp;

            Assert.That(loop.Skills.TryCast(loop.Skills.Equip(Spark)), Is.EqualTo(CastResult.Cast));

            Assert.That((hp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(KeyDamage * 2).Within(1e-6));
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(KeyDamage * 2).Within(1e-6));
            Assert.That(loop.State.QueueDamage[1].ToDouble(), Is.EqualTo(KeyDamage * 2).Within(1e-6));
            Assert.That(loop.State.QueueDamage[2].Sign, Is.EqualTo(0));
            Assert.That(TestWorld.Real.SkillBook.StartingGems("stormweaver"), Has.Some.Property("Id").EqualTo("finger_spark"));
        }

        [Test]
        public void ChainTargetsAddsOneMoreEnemyToTheChain()
        {
            var (loop, _) = Stormweaver(new Modifier(CombatStats.ChainTargets, ModifierKind.Flat, 1));
            loop.State.Resource = 50;
            TestWorld.ArmFor(loop, Spark);

            loop.Skills.TryCast(loop.Skills.Equip(Spark));

            Assert.That(loop.State.QueueDamage[2].ToDouble(), Is.EqualTo(KeyDamage * 2).Within(1e-6));
        }
    }
}
