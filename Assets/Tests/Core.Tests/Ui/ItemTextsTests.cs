using System;
using System.Linq;
using Game.Core.Content.Items;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class ItemTextsTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void ThereAreElevenRarities() =>
            Assert.That(Enum.GetValues(typeof(ItemRarity)).Length, Is.EqualTo(11));

        [Test]
        public void EveryRarityHasItsOwnName()
        {
            var rarities = Enum.GetValues(typeof(ItemRarity)).Cast<ItemRarity>().ToList();

            Assert.That(rarities.All(rarity => Texts.Has("rarity." + rarity.ToString().ToLowerInvariant())), Is.True);
            Assert.That(rarities.Select(ItemTexts.Rarity).Distinct().Count(), Is.EqualTo(rarities.Count));
            Assert.That(ItemTexts.Rarity(ItemRarity.Rift), Is.EqualTo("Разломная"));
            Assert.That(ItemTexts.Rarity(ItemRarity.Primordial), Is.EqualTo("Первозданная"));
        }

        [Test]
        public void WeaponsWithoutAffixesAreSimpleAndSaySoInTheSubtitle()
        {
            var axe = TestWorld.Real.Weapons.Starter(TestWorld.Real.Weapons.StarterFor(HeroClass.Runedancer));

            Assert.That(ItemTexts.RarityOf(axe), Is.EqualTo(ItemRarity.Simple));
            Assert.That(ItemTexts.Subtitle(axe), Does.EndWith("простая"));
        }

        private static GearAffix Affix(string id, int tier, double roll)
        {
            Assert.That(TestWorld.Real.Items.TryGetAffix(id, out var def), Is.True, id);
            return new GearAffix(def, tier, def.RollValue(tier, roll), null, roll);
        }

        [Test]
        public void AnAffixShowsItsTierRangeNextToTheNumber()
        {
            var text = ItemTexts.Affixes(new[] { Affix("armor_damage", 6, 0.6) });

            Assert.That(text, Is.EqualTo("+21% урон (18–23)"));
        }

        [Test]
        public void ALockedAffixIsMarkedAndAnOfferShowsTheOldNumber()
        {
            var before = new[] { Affix("armor_damage", 6, 0), Affix("armor_crit_power", 6, 0) };
            var after = new[] { before[0], Affix("armor_crit_power", 6, 1) };
            var offer = new RerollOffer(null, null, before, after, 0, 360);

            Assert.That(ItemTexts.Affixes(before, 0), Does.StartWith("[закреплено] +18% урон"));
            Assert.That(ItemTexts.Offer(offer), Does.EndWith("(было 30)"));
            Assert.That(ItemTexts.Offer(offer), Does.StartWith("[закреплено]"));
        }

        [Test]
        public void TheRerollButtonSaysThePriceOrWhyItCannotRoll()
        {
            Assert.That(ItemTexts.Reroll(RerollResult.Offered, 180, 500), Is.EqualTo("Перероллить · 180 Пыли"));
            Assert.That(ItemTexts.Reroll(RerollResult.NotEnoughDust, 180, 42.7), Is.EqualTo("Нужно 180 Пыли, есть 42"));
            Assert.That(ItemTexts.Reroll(RerollResult.Unique, 180, 500), Is.EqualTo("Строки уникальной не меняются"));
            Assert.That(ItemTexts.Lock(null), Is.EqualTo("Закрепить: —"));
            Assert.That(ItemTexts.Lock(1), Is.EqualTo("Закреплён: 2"));
        }
    }
}
