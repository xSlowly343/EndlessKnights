using System.Linq;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class ItemCatalogTests
    {
        private static ItemCatalog Catalog => TestWorld.Data.Items;

        [Test]
        public void TwentyFourArmourBasesAndFifteenJewelleryBases()
        {
            foreach (var slot in new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots })
            {
                var bases = Catalog.Bases.Where(b => b.Slot == slot).ToList();
                Assert.That(bases.Count, Is.EqualTo(6), slot.ToString());
                Assert.That(bases.Select(b => b.Color).Distinct().Count(), Is.EqualTo(3), slot.ToString());
            }
            Assert.That(Catalog.Bases.Count(b => b.Slot == ItemSlot.Ring), Is.EqualTo(8));
            Assert.That(Catalog.Bases.Count(b => b.Slot == ItemSlot.Amulet), Is.EqualTo(7));
            Assert.That(Catalog.Bases.All(b => b.Implicit.Count > 0), Is.True);
        }

        [TestCase(ItemRarity.Simple, 0)]
        [TestCase(ItemRarity.Fine, 1)]
        [TestCase(ItemRarity.Rare, 3)]
        [TestCase(ItemRarity.Legendary, 4)]
        [TestCase(ItemRarity.Mythic, 5)]
        [TestCase(ItemRarity.Rift, 6)]
        public void RarityGivesItsNumberOfAffixesWithMoreSuffixes(ItemRarity rarity, int count)
        {
            var rng = new Rng(3);
            for (var i = 0; i < 50; i++)
            {
                var item = Catalog.Roll(ItemSlot.Helmet, "red", rarity, 6, rng);
                Assert.That(item.Affixes.Count, Is.EqualTo(count));
                Assert.That(item.Affixes.Count(a => a.Def.Prefix), Is.EqualTo(count / 2));
            }
        }

        [Test]
        public void AffixesDoNotRepeatAndOneAttributePerItem()
        {
            var rng = new Rng(5);
            for (var i = 0; i < 200; i++)
            {
                var item = Catalog.Roll(ItemSlot.Amulet, null, ItemRarity.Rift, 6, rng);
                Assert.That(item.Affixes.Select(a => a.Def.Id).Distinct().Count(), Is.EqualTo(item.Affixes.Count));
                Assert.That(item.Affixes.Count(a => a.Def.Group == "attribute"), Is.LessThanOrEqualTo(1));
                Assert.That(item.Affixes.All(a => a.Def.FitsSlot(ItemSlot.Amulet)), Is.True);
            }
        }

        [Test]
        public void TiersNeverExceedTheItemLevel()
        {
            var rng = new Rng(7);
            for (var level = 1; level <= 6; level++)
            {
                for (var i = 0; i < 100; i++)
                {
                    var item = Catalog.Roll(ItemSlot.Boots, "green", ItemRarity.Mythic, level, rng);
                    Assert.That(item.Affixes.All(a => a.Tier >= 1 && a.Tier <= level), Is.True, $"level {level}");
                }
            }
        }

        [Test]
        public void AncientTakesOnlyTheTwoTopTiers()
        {
            var rng = new Rng(9);
            for (var i = 0; i < 200; i++)
            {
                var item = Catalog.Roll(ItemSlot.Chest, "red", ItemRarity.Ancient, 6, rng);
                Assert.That(item.Affixes.All(a => a.Tier >= 5), Is.True);
            }
        }

        [Test]
        public void TopTierIsTheMostCommon()
        {
            var rng = new Rng(11);
            var counts = new int[7];
            for (var i = 0; i < 2000; i++)
            {
                foreach (var affix in Catalog.Roll(ItemSlot.Gloves, "blue", ItemRarity.Mythic, 6, rng).Affixes)
                    counts[affix.Tier]++;
            }
            Assert.That(counts[6], Is.GreaterThan(counts[5]));
            Assert.That(counts[5], Is.GreaterThan(counts[1]));
        }

        [Test]
        public void ValuesGoEvenlyFromTheFirstToTheLastTier()
        {
            Assert.That(Catalog.TryGetAffix("armor_damage", out var affix), Is.True);

            Assert.That(affix.ValueAt(1, Catalog.Tiers), Is.EqualTo(0.03).Within(1e-12));
            Assert.That(affix.ValueAt(6, Catalog.Tiers), Is.EqualTo(0.18).Within(1e-12));
            Assert.That(affix.ValueAt(3, Catalog.Tiers), Is.EqualTo(0.09).Within(1e-12));
        }

        [Test]
        public void BaseFollowsColourAndBiome()
        {
            var rng = new Rng(13);
            for (var i = 0; i < 100; i++)
            {
                var early = Catalog.PickBase(ItemSlot.Helmet, "green", 1, rng);
                Assert.That(early.Color, Is.EqualTo("green"));
                Assert.That(early.FromBiome, Is.EqualTo(1));
            }
            var late = Enumerable.Range(0, 100).Select(_ => Catalog.PickBase(ItemSlot.Helmet, "green", 6, rng).FromBiome).Distinct().ToList();
            Assert.That(late, Does.Contain(4));
        }
    }
}
