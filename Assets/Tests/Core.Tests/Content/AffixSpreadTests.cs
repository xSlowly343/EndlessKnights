using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class AffixSpreadTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static AffixDef Affix(string id)
        {
            Assert.That(Items.TryGetAffix(id, out var affix), Is.True, id);
            return affix;
        }

        [Test]
        public void ATierRollsInItsOwnRangeOfWholePercents()
        {
            var damage = Affix("armor_damage");

            Assert.That(damage.Lowest(1), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(damage.Highest(1), Is.EqualTo(0.06).Within(1e-9));
            Assert.That(damage.Lowest(4), Is.EqualTo(0.19).Within(1e-9));
            Assert.That(damage.Highest(4), Is.EqualTo(0.31).Within(1e-9));
            Assert.That(damage.RollValue(4, 0.5), Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void RangesGrowUnevenlyTowardsTheBestTier()
        {
            var damage = Affix("armor_damage");
            var widths = Enumerable.Range(1, Items.Tiers).Select(tier => System.Math.Round(damage.Highest(tier) - damage.Lowest(tier), 6)).ToList();

            Assert.That(widths.Distinct().Count(), Is.GreaterThan(1), "tiers are not all the same width");
            Assert.That(widths[Items.Tiers - 1], Is.GreaterThan(widths[0]), "the best tier is the widest");
        }

        [Test]
        public void ABrokenRangeTableIsRejected()
        {
            var json = TestData.ReadGameDataFile(ItemCatalog.FileName);
            var table = "\"ranges\": [[0.03, 0.06], [0.07, 0.11], [0.12, 0.18], [0.19, 0.31]]";
            var overlap = json.Replace(table, "\"ranges\": [[0.03, 0.07], [0.07, 0.11], [0.12, 0.18], [0.19, 0.31]]");
            var tooShort = json.Replace(table, "\"ranges\": [[0.03, 0.06], [0.07, 0.11]]");

            Assert.That(json, Does.Contain(table));
            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(overlap))!.Message, Does.Contain("overlaps"));
            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(tooShort))!.Message, Does.Contain("tier ranges"));
        }

        [Test]
        public void SmallStepsRollInTenthsOfAPercent()
        {
            var crit = Affix("armor_crit_chance");

            Assert.That(crit.Lowest(1), Is.EqualTo(0.005).Within(1e-9));
            Assert.That(crit.Highest(1), Is.EqualTo(0.011).Within(1e-9));
            Assert.That(Affix("jewel_damage").Highest(1), Is.EqualTo(0.04).Within(1e-9), "whole percents stay whole");
        }

        [Test]
        public void TiersNeverOverlapAndAHigherTierIsAlwaysStronger()
        {
            foreach (var affix in Items.Affixes)
            {
                for (var tier = 1; tier < Items.Tiers; tier++)
                {
                    var growing = affix.TierLast >= affix.TierOne;
                    var top = affix.Highest(tier);
                    var next = affix.Lowest(tier + 1);
                    Assert.That(growing ? top <= next : top >= next, Is.True, $"{affix.Id} tier {tier}: {top} then {next}");
                }
            }
        }

        [Test]
        public void ArmorJewelryAndWeaponAffixesHaveASpread()
        {
            var spread = Items.Affixes.Where(affix => affix.Highest(Items.Tiers) != affix.Lowest(Items.Tiers)).Select(affix => affix.Id).ToList();

            foreach (var id in new[] { "armor_damage", "helmet_resource_gain", "gloves_skill_cooldown", "jewel_gold", "ring_item_rarity", "weapon_strike_damage", "gear_resource_max" })
                Assert.That(spread, Does.Contain(id));
            foreach (var id in new[] { "helmet_support_level", "weapon_fast_level", "weapon_socket_left", "amulet_extra_aura" })
                Assert.That(spread, Does.Not.Contain(id), "whole levels and sockets do not spread");
        }

        [Test]
        public void RolledAffixesCoverTheWholeRange()
        {
            var rng = new Rng(11);
            var values = new HashSet<double>();
            for (var i = 0; i < 3000; i++)
            {
                foreach (var affix in Items.RollAffixes(ItemSlot.Helmet, ItemRarity.Mythic, 6, rng))
                {
                    if (affix.Def.Id == "armor_damage" && affix.Tier == Items.Tiers)
                        values.Add(System.Math.Round(affix.Value, 6));
                }
            }

            Assert.That(values, Is.EquivalentTo(Enumerable.Range(19, 13).Select(percent => percent / 100.0)));
        }

        [Test]
        public void TheSecondLineOfAnUltimateAffixRollsToo()
        {
            var level = Affix("helmet_aura_level");
            var low = new List<Modifier>();
            var high = new List<Modifier>();

            new GearAffix(level, Items.Tiers, level.Lowest(Items.Tiers), roll: 0).AddModifiers(low);
            new GearAffix(level, Items.Tiers, level.Lowest(Items.Tiers), roll: 1).AddModifiers(high);

            Assert.That(low[0].Value, Is.EqualTo(high[0].Value), "the level is whole and does not spread");
            Assert.That(high[1].Value, Is.GreaterThan(low[1].Value));
            Assert.That(low[1].Value, Is.EqualTo(0.26).Within(1e-9), "a roll of 0 is the bottom of the tier");
        }
    }
}
