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
            var axe = TestWorld.Real.Weapons.StarterFor(HeroClass.Viking);

            Assert.That(ItemTexts.RarityOf(axe), Is.EqualTo(ItemRarity.Simple));
            Assert.That(ItemTexts.Subtitle(axe), Does.EndWith("простая"));
        }
    }
}
