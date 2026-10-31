using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HugeDamageTests
    {
        private static StrikeUpgradeRules Rules => TestWorld.Data.Combat.StrikeUpgrade;

        [Test]
        public void MoreMultipliersPastTheDoubleRangeStillGiveDamage()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e200));
            sheet.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e200));
            sheet.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, 1));

            var damage = sheet.Compute(CombatStats.Damage, BigNumber.FromDouble(5));

            Assert.That(damage.Log10(), Is.EqualTo(400 + System.Math.Log10(10)).Within(1e-6));
        }

        [Test]
        public void SmallMultipliersKeepTheExactPath()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 0.5));

            Assert.That(sheet.Compute(CombatStats.Damage, BigNumber.FromDouble(2)).ToDouble(), Is.EqualTo(3));
        }

        [Test]
        public void AStrikeLevelPastTheDoubleRangeSplitsIntoSeveralMultipliers()
        {
            const int level = 40_000;
            var loop = TestWorld.Loop();
            loop.State.StrikeLevel = level;
            loop.Upgrades.Rebuild();

            var damage = loop.Hero.Compute(CombatStats.Damage, BigNumber.FromDouble(1), StatTags.Keyboard);

            Assert.That(Rules.DamageMultiplierLog10(level), Is.GreaterThan(400));
            Assert.That(loop.Upgrades.Modifiers.Count, Is.GreaterThan(1));
            Assert.That(damage.Log10(), Is.GreaterThan(Rules.DamageMultiplierLog10(level) - 1));
        }

        [Test]
        public void TheLog10OfTheStrikeMultiplierMatchesTheMultiplier()
        {
            Assert.That(Rules.DamageMultiplierLog10(60), Is.EqualTo(System.Math.Log10(Rules.DamageMultiplier(60))).Within(1e-9));
        }
    }
}
