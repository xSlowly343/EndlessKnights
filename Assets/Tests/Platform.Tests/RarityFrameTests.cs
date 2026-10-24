using System.Linq;
using Game.Core.Content.Items;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Platform.Tests
{
    public class RarityFrameTests
    {
        private RectTransform _host = null!;
        private RarityFrame _frame = null!;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>();
            _host.sizeDelta = new Vector2(58f, 54f);
            _frame = RarityFrame.Create(_host, 8f, 2f, Color.black);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host.gameObject);

        [Test]
        public void EpicFrameHasItsColorAndNoGlow()
        {
            _frame.Show(ItemRarity.Epic, true);

            Assert.That(Part("Frame").color, Is.EqualTo(RarityFrame.Of(ItemRarity.Epic)));
            Assert.That(Part("Glow").color.a, Is.Zero);
        }

        [Test]
        public void LegendaryAndAboveGlowUnlessSelected()
        {
            _frame.Show(ItemRarity.Legendary, true);
            Assert.That(Part("Glow").color.a, Is.GreaterThan(0f));

            _frame.Show(ItemRarity.Legendary, false);
            Assert.That(Part("Glow").color.a, Is.Zero);
        }

        [Test]
        public void RiftFrameIsRainbow()
        {
            _frame.Show(ItemRarity.Rift, true);
            Assert.That(Part("Frame").sprite, Is.SameAs(UiSprites.RainbowRounded));

            _frame.Show(ItemRarity.Rare, true);
            Assert.That(Part("Frame").sprite, Is.SameAs(UiSprites.Rounded));
        }

        [Test]
        public void AstralFrameHasStarsAndARim()
        {
            _frame.Show(ItemRarity.Astral, true);

            Assert.That(Stars().All(star => star.gameObject.activeSelf), Is.True);
            Assert.That(Part("Outline").color.a, Is.GreaterThan(0f));

            _frame.Show(ItemRarity.Primordial, true);
            Assert.That(Stars().Any(star => star.gameObject.activeSelf), Is.False);
            Assert.That(Part("Outline").color.a, Is.GreaterThan(0f));
        }

        [Test]
        public void EveryRarityHasItsOwnFrameColor()
        {
            var colors = System.Enum.GetValues(typeof(ItemRarity)).Cast<ItemRarity>().Select(RarityFrame.Of).Distinct();

            Assert.That(colors.Count(), Is.EqualTo(11));
        }

        private Image Part(string name) => _host.GetComponentsInChildren<Image>(true).First(image => image.name == name);

        private Image[] Stars() => _host.GetComponentsInChildren<Image>(true).Where(image => image.name == "Star").ToArray();
    }
}
