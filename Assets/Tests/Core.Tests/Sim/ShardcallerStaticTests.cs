using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ShardcallerStaticTests
    {
        private static ClassResource Static => TestWorld.Data.Combat.Resources.For("shardcaller");
        private static GemDef Spark => TestWorld.Real.Gems["shardcaller_hit_1"];
        private static double StrikeDamage => TestWorld.Data.Combat.StrikeDamage;

        private static (GameLoop Loop, FakeInputSource Input) Shardcaller(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Shardcaller }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        [Test]
        public void ResonanceComesFromHitsButNotFromTheMouse()
        {
            var (loop, input) = Shardcaller();

            for (var i = 0; i < 5; i++)
            {
                input.PressKeys(1);
                input.MoveMouse(400);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }

            Assert.That(Static.MousePxPerPoint, Is.Zero);
            Assert.That(loop.State.Resource, Is.EqualTo(5 * Static.PerKey).Within(0.05));
        }

        [Test]
        public void FullStaticDischargesByItselfAndEmptiesTheBar()
        {
            var (loop, _) = Shardcaller();
            loop.State.Resource = TestWorld.Data.Combat.Resources.Max;
            var hp = loop.State.EnemyHp;

            loop.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(1));
            Assert.That(loop.State.Resource, Is.EqualTo(0).Within(1e-9));
            Assert.That((hp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(StrikeDamage * Static.OverloadMultiplier).Within(1e-6));
        }

        [Test]
        public void OverloadMultiplierAndOverloadDisabledChangeTheDischarge()
        {
            var (stronger, _) = Shardcaller(new Modifier(CombatStats.OverloadMultiplier, ModifierKind.Flat, 3));
            var (off, _) = Shardcaller(new Modifier(CombatStats.OverloadDisabled, ModifierKind.Flat, 1));
            stronger.State.Resource = TestWorld.Data.Combat.Resources.Max;
            off.State.Resource = TestWorld.Data.Combat.Resources.Max;
            var hp = stronger.State.EnemyHp;

            stronger.Tick();
            off.Tick();

            Assert.That((hp - stronger.State.EnemyHp).ToDouble(), Is.EqualTo(StrikeDamage * (Static.OverloadMultiplier + 3)).Within(1e-6));
            Assert.That(off.Overload.Discharges, Is.EqualTo(0));
            Assert.That(off.State.Resource, Is.EqualTo(TestWorld.Data.Combat.Resources.Max).Within(1e-9));
        }

        [Test]
        public void OtherCharactersNeverOverload()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Minstrel }, new StatSheet());
            loop.State.Resource = TestWorld.Data.Combat.Resources.Max;

            loop.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(0));
        }
    }
}
