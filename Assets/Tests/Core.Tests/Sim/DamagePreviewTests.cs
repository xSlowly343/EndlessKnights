using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DamagePreviewTests
    {
        private static GameLoop Fresh(GameState? state = null)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), state ?? new GameState(1));
            loop.Tick();
            return loop;
        }

        private static GearItem Coif(ItemRarity rarity)
        {
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var gearBase), Is.True);
            return new GearItem(gearBase, rarity, 1, System.Array.Empty<GearAffix>());
        }

        [Test]
        public void TheBreakdownMultipliesBackToTheSheetHit()
        {
            var loop = Fresh();
            loop.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.5));
            loop.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.2).InPool(ModifierPool.Tree));
            loop.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 0.3));
            loop.Hero.Add(new Modifier(CombatStats.AdditionalDamage, ModifierKind.Flat, 0.1));
            loop.RefreshClassAttribute();

            var breakdown = DamageBreakdown.Of(loop);
            var sheetHit = loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, 1, StatTags.Strike).ToDouble();

            Assert.That(breakdown.Increased, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(breakdown.TreeBasket, Is.EqualTo(1.2).Within(1e-9));
            Assert.That(breakdown.More, Is.EqualTo(1.3).Within(1e-9));
            Assert.That(breakdown.Additional, Is.EqualTo(1.1).Within(1e-9));
            Assert.That(breakdown.Strike * breakdown.Additional * breakdown.Increased * breakdown.TreeBasket * breakdown.More, Is.EqualTo(sheetHit).Within(sheetHit * 1e-9));
            Assert.That(breakdown.Attack, Is.EqualTo(loop.Combat.Attack));
            Assert.That(breakdown.Attribute, Is.EqualTo(loop.Combat.AttributeTotal));
        }

        [Test]
        public void CritAverageWeighsChanceAndOvercrit()
        {
            var loop = Fresh();
            loop.Hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.Flat, 2));
            var combat = loop.Combat;
            var crit = combat.CritMultiplier(StatTags.Strike);
            var expected = 1 + (crit - 1) + combat.Overcrit(StatTags.Strike) * crit * (combat.OvercritMultiplier(StatTags.Strike) - 1);

            Assert.That(DamageBreakdown.Of(loop).Crit, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void TheElementFactorDoesNotTouchTheHitPortions()
        {
            var loop = Fresh();
            loop.Hero.Add(new Modifier(ElementRules.Conversion[(int)Element.Fire], ModifierKind.Flat, 0.5));

            var factor = DamageBreakdown.Of(loop).Element;

            Assert.That(factor, Is.EqualTo(loop.Combat.ElementFactor(StatTags.Strike)));
            Assert.That(loop.Combat.Portion(Element.Fire), Is.Zero);
        }

        [Test]
        public void ABetterWeaponShowsItsGainWithoutChangingTheHero()
        {
            var loop = Fresh();
            var weapon = loop.Weapons.Give(TestWorld.Data.Weapons.Bases[0], ItemRarity.Legendary);
            var held = loop.State.Weapons.Held;
            var attack = loop.Combat.Attack;

            var change = DamagePreview.Weapon(loop, weapon);

            Assert.That(change.Possible, Is.True);
            Assert.That(change.Hit, Is.GreaterThan(1));
            Assert.That(change.After.WeaponDamage, Is.GreaterThan(change.Before.WeaponDamage));
            Assert.That(loop.State.Weapons.Held, Is.SameAs(held));
            Assert.That(loop.Combat.Attack, Is.EqualTo(attack));
            Assert.That(loop.State.Weapons.Bag, Does.Contain(weapon));
        }

        [Test]
        public void TheGainMatchesWhatEquippingReallyGives()
        {
            var loop = Fresh();
            var weapon = loop.Weapons.Give(TestWorld.Data.Weapons.Bases[0], ItemRarity.Epic);
            var change = DamagePreview.Weapon(loop, weapon);
            var before = DamageBreakdown.Of(loop).Hit;

            loop.Weapons.Equip(weapon);
            loop.RefreshClassAttribute();

            Assert.That(DamageBreakdown.Of(loop).Hit / before, Is.EqualTo(change.Hit).Within(1e-9));
        }

        [Test]
        public void ArmourRaisesTheAttribute()
        {
            var loop = Fresh();
            var coif = Coif(ItemRarity.Epic);
            loop.State.Drops.Gear.Add(coif);

            var change = DamagePreview.Gear(loop, coif);

            Assert.That(change.Possible, Is.True);
            Assert.That(change.After.Attribute, Is.GreaterThan(change.Before.Attribute));
            Assert.That(change.Hit, Is.GreaterThan(1));
            Assert.That(loop.State.Gear.In(GearSlot.Helmet), Is.Null);
        }

        [Test]
        public void AnItemOutsideTheBagIsNotPreviewed()
        {
            var loop = Fresh();
            Assert.That(DamagePreview.Gear(loop, Coif(ItemRarity.Rare)).Possible, Is.False);
        }

        [Test]
        public void AFacetShowsItsGroupAndCostsNothing()
        {
            var loop = Fresh();
            for (var i = 0; i < MountRules.Slots; i++)
                loop.State.Mount.Levels[i] = 5;
            loop.Facets.Rebuild();

            var change = DamagePreview.Facet(loop, 0, 0, "edge");

            Assert.That(change.Possible, Is.True);
            Assert.That(change.After.Increased / change.Before.Increased, Is.GreaterThan(1));
            Assert.That(loop.Facets.Chosen(0, 0), Is.Empty);
            Assert.That(DamagePreview.Facet(loop, 0, 1, "edge").Possible, Is.False);
        }

        [Test]
        public void AMountLevelOnTheWeaponGrowsTheWeaponDamage()
        {
            var loop = Fresh();

            var change = DamagePreview.MountLevel(loop, MountSystem.WeaponSlot);

            Assert.That(change.After.WeaponDamage / change.Before.WeaponDamage, Is.EqualTo(TestWorld.Data.Combat.Mount.Step).Within(1e-9));
            Assert.That(loop.State.Mount.Levels[MountSystem.WeaponSlot], Is.Zero);
        }

        [Test]
        public void ATreeNodeIsPreviewedWithoutAFreePoint()
        {
            var loop = Fresh(new GameState(1) { Class = HeroClass.Runedancer });
            loop.State.TreePointsEarned = 0;
            loop.RebuildTree();

            var change = DamagePreview.Node(loop, "runedancer_r1c0");

            Assert.That(change.Possible, Is.True);
            Assert.That(loop.State.ClassTree.RankOf("runedancer_r1c0"), Is.Zero);
            Assert.That(DamagePreview.Node(loop, "no_such_node").Possible, Is.False);
        }
    }
}
