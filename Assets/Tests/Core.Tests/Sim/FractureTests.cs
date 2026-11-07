using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class FractureTests
    {
        private static FractureRules Rules => TestWorld.Data.Combat.Fracture;

        private static (GameLoop Loop, FakeInputSource Input) Fighting(StatSheet hero)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(7), hero);
            loop.Tick();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 100));
            return (loop, input);
        }

        private static StatSheet Breaker(double chance)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.FractureChance, ModifierKind.Flat, chance));
            return hero;
        }

        private static void Strike(GameLoop loop, FakeInputSource input, int times)
        {
            for (var i = 0; i < times; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
        }

        [Test]
        public void TwentyFiveStacksAreTheDraftCap()
        {
            Assert.That(Rules.PerStack, Is.EqualTo(0.02));
            Assert.That(Rules.BaseMax, Is.EqualTo(25));
            Assert.That(Rules.MaxCap, Is.EqualTo(40));
            Assert.That(Rules.DecaySeconds, Is.EqualTo(2));
        }

        [Test]
        public void EveryHitLaysAStackUpToTheCap()
        {
            var (loop, input) = Fighting(Breaker(1));
            Strike(loop, input, 10);
            Assert.That(loop.State.FractureStacks, Is.EqualTo(10));
            Strike(loop, input, 30);
            Assert.That(loop.State.FractureStacks, Is.EqualTo(25));
            Assert.That(loop.Combat.FractureBonus, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void DepthRaisesTheCapUpToForty()
        {
            var hero = Breaker(1);
            hero.Add(new Modifier(CombatStats.FractureMax, ModifierKind.Flat, 5));
            var (loop, input) = Fighting(hero);
            Strike(loop, input, 40);
            Assert.That(loop.State.FractureStacks, Is.EqualTo(30));
            hero.Add(new Modifier(CombatStats.FractureMax, ModifierKind.Flat, 100));
            Strike(loop, input, 40);
            Assert.That(loop.State.FractureStacks, Is.EqualTo(40));
        }

        [Test]
        public void StrengthMakesEachStackHeavier()
        {
            var hero = Breaker(1);
            hero.Add(new Modifier(CombatStats.FractureEffect, ModifierKind.Flat, 0.5));
            var (loop, input) = Fighting(hero);
            Strike(loop, input, 10);
            Assert.That(loop.Combat.FractureBonus, Is.EqualTo(10 * 0.03).Within(1e-9));
        }

        [Test]
        public void StacksRaiseTheDamageTaken()
        {
            var (plain, plainInput) = Fighting(new StatSheet());
            var (broken, brokenInput) = Fighting(new StatSheet());
            broken.State.FractureStacks = 25;
            Strike(plain, plainInput, 1);
            Strike(broken, brokenInput, 1);
            Assert.That((broken.Combat.LastTickDamage / plain.Combat.LastTickDamage).ToDouble(), Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void WithoutHitsAStackFallsOffEveryTwoSeconds()
        {
            var (loop, input) = Fighting(Breaker(1));
            Strike(loop, input, 5);
            var ticks = (int)System.Math.Round(Rules.DecaySeconds * GameLoop.TicksPerSecond);
            for (var i = 0; i < ticks - 1; i++)
                loop.Tick();
            Assert.That(loop.State.FractureStacks, Is.EqualTo(5));
            loop.Tick();
            Assert.That(loop.State.FractureStacks, Is.EqualTo(4));
            for (var i = 0; i < ticks; i++)
                loop.Tick();
            Assert.That(loop.State.FractureStacks, Is.EqualTo(3));
        }

        [Test]
        public void ANewEnemyStartsWhole()
        {
            var (loop, input) = Fighting(Breaker(1));
            Strike(loop, input, 5);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 100));
            Assert.That(loop.State.FractureStacks, Is.Zero);
        }

        [Test]
        public void AHalfChanceLaysAboutHalfTheStacks()
        {
            var hero = Breaker(0.5);
            hero.Add(new Modifier(CombatStats.FractureMax, ModifierKind.Flat, 100));
            var (loop, input) = Fighting(hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 300));
            Strike(loop, input, 60);
            Assert.That(loop.State.FracturesLaid, Is.InRange(18, 40));
        }

        [Test]
        public void OnASharedEnemyTheStacksGoToTheHostScaledByCohesion()
        {
            var hero = Breaker(1);
            hero.Add(new Modifier(CombatStats.Cohesion, ModifierKind.Flat, 0.2));
            var (loop, input) = Fighting(hero);
            loop.Skills.ShareEnemy(true);
            Strike(loop, input, 10);
            Assert.That(loop.Combat.FractureBonus, Is.EqualTo(0.2).Within(1e-9));
            var found = 0.0;
            var effects = loop.State.TraitEnemyEffects;
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i].Id == CombatSystem.FractureEffectId)
                    found = effects[i].Strength;
            }
            Assert.That(found, Is.GreaterThan(0).And.LessThanOrEqualTo(0.2 * 1.2 + 1e-9), "the stacks go out as an enemy effect, scaled by cohesion");
        }

        [Test]
        public void PillarCountsThePartyUpToFour()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Pillar, ModifierKind.Flat, 0.05));
            var (loop, _) = Fighting(hero);
            Assert.That(loop.Combat.PillarBonus, Is.Zero, "alone there is no party to lean on");
            loop.State.PartyMembers = 3;
            Assert.That(loop.Combat.PillarBonus, Is.EqualTo(0.15).Within(1e-9));
            loop.State.PartyMembers = 6;
            Assert.That(loop.Combat.PillarBonus, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void TheChipFacetLaysFracture()
        {
            var option = TestWorld.Data.Combat.Facets.Slots[3][2];
            Assert.That(option.Id, Is.EqualTo("chip"));
            Assert.That(option.Lines[0].Stat, Is.EqualTo(CombatStats.FractureChance));
        }
    }
}
