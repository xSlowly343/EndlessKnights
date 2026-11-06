using System.Linq;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GuardianMechanicTests
    {
        private static EnemyDef Guardian(string id) => TestWorld.Data.Enemies[id];

        private static (GameState State, GuardianWatch Watch) Fight(string id)
        {
            var state = new GameState();
            state.SpawnEnemy(Guardian(id), EnemyType.Guardian, BigNumber.FromDouble(1000));
            return (state, new GuardianWatch(state));
        }

        private static bool Has(ulong conditions, ModifierCondition condition) => (conditions & ModifierConditions.Bit(condition)) != 0;

        [Test]
        public void EveryHostOfABiomeHasItsOwnRule()
        {
            foreach (var id in new[] { "sphinx_warden", "titan_mammoth", "flame_smith", "quiet_storm_dragon" })
                Assert.That(Guardian(id).Mechanic, Is.Not.Null, id);
            Assert.That(Guardian("storm_dragon").Modifiers.Any(m => m.When == ModifierCondition.MouseMoving), Is.True);
        }

        [Test]
        public void TheSphinxShieldHoldsUntilTheTopOfItsLifeIsGone()
        {
            var (state, watch) = Fight("sphinx_warden");
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianShield), Is.True);
            state.EnemyHp = BigNumber.FromDouble(1000 * (1 - Guardian("sphinx_warden").Mechanic!.Value) - 1);
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianShield), Is.False);
        }

        [Test]
        public void TheShieldBlocksKeysAndMouseButNotClicks()
        {
            var hero = new StatSheet();
            foreach (var modifier in Guardian("sphinx_warden").Modifiers)
                hero.Add(modifier);
            hero.SetConditions(ModifierConditions.Bit(ModifierCondition.GuardianShield));
            Assert.That(hero.Compute(CombatStats.Damage, 100, StatTags.Strike).ToDouble(), Is.EqualTo(25).Within(1e-9));
            Assert.That(hero.Compute(CombatStats.Damage, 100, StatTags.Mouse).ToDouble(), Is.EqualTo(25).Within(1e-9));
            Assert.That(hero.Compute(CombatStats.Damage, 100, StatTags.Click).ToDouble(), Is.EqualTo(100).Within(1e-9));
            hero.SetConditions(0);
            Assert.That(hero.Compute(CombatStats.Damage, 100, StatTags.Strike).ToDouble(), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void TheMammothWoolIsWetForAFewSecondsAfterASkill()
        {
            var (state, watch) = Fight("titan_mammoth");
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianWet), Is.False);
            state.SkillCasts++;
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianWet), Is.True);
            state.Tick += (long)(Guardian("titan_mammoth").Mechanic!.Value * GameLoop.TicksPerSecond);
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianWet), Is.False);
        }

        [Test]
        public void TheSmithKeepsItsRhythmUntilTheMouseBreaksIt()
        {
            var (state, watch) = Fight("flame_smith");
            state.TotalInput.MouseMeters += 1000;
            var (fresh, freshWatch) = Fight("flame_smith");
            Assert.That(Has(freshWatch.Conditions(), ModifierCondition.GuardianRhythm), Is.True);
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianRhythm), Is.True, "mouse moved before the fight does not count");
            state.TotalInput.MouseMeters += Guardian("flame_smith").Mechanic!.Value;
            Assert.That(Has(watch.Conditions(), ModifierCondition.GuardianRhythm), Is.False);
            Assert.That(fresh.EnemyAlive, Is.True);
        }

        [Test]
        public void TheQuietDragonSilencesKeysMouseAndClicksInTurn()
        {
            var (state, watch) = Fight("quiet_storm_dragon");
            var period = (long)(Guardian("quiet_storm_dragon").Mechanic!.Value * GameLoop.TicksPerSecond);
            Assert.That(Has(watch.Conditions(), ModifierCondition.SilenceKeys), Is.True);
            state.Tick += period;
            Assert.That(Has(watch.Conditions(), ModifierCondition.SilenceMouse), Is.True);
            state.Tick += period;
            Assert.That(Has(watch.Conditions(), ModifierCondition.SilenceClicks), Is.True);
            state.Tick += period;
            Assert.That(Has(watch.Conditions(), ModifierCondition.SilenceKeys), Is.True);
        }

        [Test]
        public void OrdinaryEnemiesSetNoGuardianConditions()
        {
            var state = new GameState();
            state.SpawnEnemy(TestWorld.Data.Enemies["goblin_giant"], EnemyType.Normal, BigNumber.FromDouble(10));
            Assert.That(new GuardianWatch(state).Conditions(), Is.EqualTo(0UL));
        }
    }
}
