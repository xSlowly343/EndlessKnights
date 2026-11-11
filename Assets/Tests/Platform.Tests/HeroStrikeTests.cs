using Game.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class HeroStrikeTests
    {
        private GameObject _scene = null!;
        private HeroStrike _strike = null!;

        [SetUp]
        public void SetUp()
        {
            _scene = new GameObject("Scene");
            _strike = new HeroStrike("Strike", new Vector2(6f, 2f), 3f, 11, 11, 0f, _scene.transform);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_scene);

        [Test]
        public void CrescentFliesToTheTargetAndBurstsThere()
        {
            var target = new Vector2(28f, 5f);

            _strike.Launch(target);
            _strike.Animate(0.1f);

            Assert.That(_strike.Flying, Is.EqualTo(1));
            Assert.That(_strike.Bursting, Is.Zero);
            var crescent = _scene.transform.Find("Strike crescent 0");
            Assert.That(crescent.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(crescent.position.x, Is.GreaterThan(6f).And.LessThan(28f));

            _strike.Animate(HeroModel.Crescent.Duration);

            Assert.That(_strike.Flying, Is.Zero);
            Assert.That(_strike.Bursting, Is.EqualTo(1));
            Assert.That(crescent.GetComponent<SpriteRenderer>().enabled, Is.False);
            var burst = _scene.transform.Find("Strike burst 0");
            Assert.That(burst.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That((Vector2)burst.position, Is.EqualTo(target));

            _strike.Animate(HeroModel.Burst.Duration);

            Assert.That(_strike.Bursting, Is.Zero);
            Assert.That(burst.GetComponent<SpriteRenderer>().enabled, Is.False);
        }

        [Test]
        public void SlashShowsOnlyFromTheHitFrameToTheEndOfTheSwing()
        {
            _strike.ShowSlash(0.05f);
            Assert.That(_strike.SlashShown, Is.False);

            _strike.ShowSlash(HeroModel.StrikeSeconds + 0.01f);
            Assert.That(_strike.SlashShown, Is.True);

            _strike.ShowSlash(HeroModel.Attack.Duration + 0.01f);
            Assert.That(_strike.SlashShown, Is.False);
        }

        [Test]
        public void OpacityFadesEveryLayerAndZeroHidesThem()
        {
            _strike.UseOpacity(0.4f);
            _strike.Launch(new Vector2(28f, 5f));
            _strike.Animate(0.1f);
            _strike.ShowSlash(HeroModel.StrikeSeconds + 0.01f);

            var crescent = _scene.transform.Find("Strike crescent 0").GetComponent<SpriteRenderer>();
            var slash = _scene.transform.Find("Strike slash").GetComponent<SpriteRenderer>();
            Assert.That(_strike.Opacity, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(crescent.color.a, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(slash.color.a, Is.EqualTo(0.4f).Within(0.001f));

            _strike.UseOpacity(0f);
            _strike.Animate(0.05f);
            _strike.ShowSlash(HeroModel.StrikeSeconds + 0.02f);

            Assert.That(crescent.enabled, Is.False);
            Assert.That(_strike.SlashShown, Is.False);
        }

        [Test]
        public void TintKeepsTheChosenOpacity()
        {
            _strike.UseOpacity(0.5f);
            _strike.UseTint(new Color(0.8f, 0.9f, 1f));

            var slash = _scene.transform.Find("Strike slash").GetComponent<SpriteRenderer>();
            Assert.That(slash.color.b, Is.EqualTo(1f));
            Assert.That(slash.color.r, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(slash.color.a, Is.EqualTo(0.5f).Within(0.001f));
        }
    }
}
