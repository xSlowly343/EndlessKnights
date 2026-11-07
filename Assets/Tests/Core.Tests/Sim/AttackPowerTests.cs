using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AttackPowerTests
    {
        private static GameLoop Fresh(StatSheet? hero = null)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1), hero);
            loop.Tick();
            return loop;
        }

        [Test]
        public void TheStartingHeroHitsAsBefore()
        {
            var loop = Fresh();
            var rules = TestWorld.Data.Combat.Attack;
            Assert.That(rules.BaseAttack, Is.EqualTo(System.Math.Sqrt(rules.BaseAttribute * rules.BaseWeaponDamage / rules.Divisor)).Within(1e-9));
            Assert.That(loop.Combat.AttackScale, Is.EqualTo(1).Within(1e-12));
            Assert.That(loop.Combat.BaseKeyDamage, Is.EqualTo(TestWorld.Data.Combat.StrikeDamage).Within(1e-12));
        }

        [Test]
        public void ABetterWeaponRaisesTheAttackUnderTheRoot()
        {
            var loop = Fresh();
            var rules = TestWorld.Data.Combat.Attack;
            var weapon = loop.Weapons.Give(TestWorld.Data.Weapons.Bases[0], ItemRarity.Rare);
            loop.Weapons.Equip(weapon);
            loop.Tick();
            var expected = System.Math.Sqrt(rules.WeaponDamage(ItemRarity.Rare, 1) / rules.BaseWeaponDamage);
            Assert.That(loop.Combat.AttackScale, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void ItemLevelGrowsTheWeaponDamageByAMultiplier()
        {
            var rules = TestWorld.Data.Combat.Attack;
            Assert.That(rules.WeaponDamage(ItemRarity.Simple, 11) / rules.WeaponDamage(ItemRarity.Simple, 1), Is.EqualTo(System.Math.Pow(rules.PerItemLevel, 10)).Within(1e-9));
            Assert.That(rules.Attribute(ItemRarity.Epic, 1), Is.GreaterThan(rules.Attribute(ItemRarity.Simple, 1)));
        }

        [Test]
        public void ArmourAndTreePointsFeedTheAttribute()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(Game.Core.Content.Tree.ClassAttributeDef.Stat, ModifierKind.Flat, 300));
            var loop = Fresh(hero);
            var rules = TestWorld.Data.Combat.Attack;
            Assert.That(loop.Combat.AttackScale, Is.EqualTo(System.Math.Sqrt((rules.BaseAttribute + 300) / rules.BaseAttribute)).Within(1e-9));
        }

        [Test]
        public void FlatAttackAddsOnTop()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.FlatAttack, ModifierKind.Flat, 50));
            var loop = Fresh(hero);
            Assert.That(loop.Combat.Attack, Is.EqualTo(TestWorld.Data.Combat.Attack.BaseAttack + 50).Within(1e-9));
        }

        [Test]
        public void EnemyHpFollowsTheItemLevel()
        {
            var stages = TestWorld.Real.Stages;
            Assert.That(stages.ItemLevelRate, Is.EqualTo(TestWorld.Real.Combat.Attack.PerItemLevel), "a hero with gear of the stage keeps the same pace");
            Assert.That(stages.ItemLevelAt(50), Is.EqualTo(1));
            Assert.That(stages.ItemLevelAt(51), Is.EqualTo(2));
        }
    }
}
