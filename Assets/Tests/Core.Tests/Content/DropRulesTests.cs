using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class DropRulesTests
    {
        private static string Json => TestData.ReadGameDataFile(DropRules.FileName);

        [Test]
        public void ShippedFileMatchesTheItemsDocument()
        {
            var rules = DropRules.Load(Json);

            Assert.That(rules.Daily.Limit, Is.EqualTo(20));
            Assert.That(rules.Daily.MaxLimit, Is.EqualTo(25));
            Assert.That(rules.Daily.Chance, Is.EqualTo(0.25));
            Assert.That(rules.Daily.RecordShare, Is.EqualTo(0.8));
            Assert.That(rules.Record.MaxRarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(rules.SpecialBoss.MinRarity, Is.EqualTo(ItemRarity.Uncommon));
            Assert.That(rules.Guardian.Items, Is.EqualTo(2));
            Assert.That(rules.BagSize, Is.EqualTo(60));
            Assert.That(rules.GuaranteeAt(3)!.Slot, Is.EqualTo(ItemSlot.Helmet));
            Assert.That(rules.GuaranteeAt(50)!.ToEmptySlot, Is.True);
            Assert.That(rules.GuaranteeAt(4), Is.Null);
            Assert.That(rules.GemChance(GemKind.Skill).Normal, Is.EqualTo(1 / 300.0));
            Assert.That(rules.GemChance(GemKind.Skill).Boss, Is.EqualTo(0.1));
        }

        [Test]
        public void SlotWeightsGiveArmorJewelryAndWeaponsTheirShares()
        {
            var weights = DropRules.Load(Json).SlotWeights;
            var total = 0.0;
            foreach (var weight in weights)
                total += weight;

            var armor = weights[(int)ItemSlot.Helmet] + weights[(int)ItemSlot.Chest] + weights[(int)ItemSlot.Gloves] + weights[(int)ItemSlot.Boots];
            Assert.That(armor / total, Is.EqualTo(0.55).Within(1e-9));
            Assert.That((weights[(int)ItemSlot.Ring] + weights[(int)ItemSlot.Amulet]) / total, Is.EqualTo(0.25).Within(1e-9));
            Assert.That(weights[(int)ItemSlot.Weapon] / total, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void RarityWeightsSumToOneHundredPercent()
        {
            var rules = DropRules.Load(Json);
            var total = 0.0;
            for (var rarity = 0; rarity < DropRules.RarityCount; rarity++)
                total += rules.RarityWeight((ItemRarity)rarity);

            Assert.That(total, Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void UnknownRarityIsRejected()
        {
            var json = Json.Replace("\"rift\": 0.01 },", "\"rift\": 0.01, \"astral\": 1 },");

            var error = Assert.Throws<ContentException>(() => DropRules.Load(json))!;
            Assert.That(error.Message, Does.Contain("astral"));
        }

        [Test]
        public void LimitAboveTheMaximumIsRejected()
        {
            Assert.Throws<ContentException>(() => DropRules.Load(Json.Replace("\"limit\": 20", "\"limit\": 30")));
        }

        [Test]
        public void ArmorOfAnotherClassIsRejected()
        {
            var json = Json.Replace("\"slot\": \"helmet\", \"rarity\": \"fine\", \"color\": \"own\"", "\"slot\": \"helmet\", \"rarity\": \"fine\", \"color\": \"ownColorOtherClass\"");

            Assert.Throws<ContentException>(() => DropRules.Load(json));
        }

        [Test]
        public void GemChanceBelowOneInOneIsRejected()
        {
            Assert.Throws<ContentException>(() => DropRules.Load(Json.Replace("\"normal\": 300", "\"normal\": 0.5")));
        }
    }
}
