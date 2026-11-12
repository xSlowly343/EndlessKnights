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

            _strike.Launch();
            _strike.Animate(0.05f, target);

            Assert.That(_strike.Flying, Is.EqualTo(1));
            Assert.That(_strike.Bursting, Is.Zero);
            var crescent = _scene.transform.Find("Strike crescent 0");
            Assert.That(crescent.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(crescent.position.x, Is.GreaterThan(6f).And.LessThan(28f));

            _strike.Animate(HeroStrike.FlightSeconds, target);

            Assert.That(_strike.Flying, Is.Zero);
            Assert.That(_strike.Bursting, Is.EqualTo(1));
            Assert.That(crescent.GetComponent<SpriteRenderer>().enabled, Is.False);
            var burst = _scene.transform.Find("Strike burst 0");
            Assert.That(burst.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That((Vector2)burst.position, Is.EqualTo(target));

            _strike.Animate(HeroModel.Burst.Duration, target);

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
            _strike.Launch();
            _strike.Animate(0.05f, new Vector2(28f, 5f));
            _strike.ShowSlash(HeroModel.StrikeSeconds + 0.01f);

            var crescent = _scene.transform.Find("Strike crescent 0").GetComponent<SpriteRenderer>();
            var slash = _scene.transform.Find("Strike slash").GetComponent<SpriteRenderer>();
            Assert.That(_strike.Opacity, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(crescent.color.a, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(slash.color.a, Is.EqualTo(0.4f).Within(0.001f));

            _strike.UseOpacity(0f);
            _strike.Animate(0.05f, new Vector2(28f, 5f));
            _strike.ShowSlash(HeroModel.StrikeSeconds + 0.02f);

            Assert.That(crescent.enabled, Is.False);
            Assert.That(_strike.SlashShown, Is.False);
        }

        [Test]
        public void AnotherStrikeArtSwapsTheSheetsAndStopsWhatWasFlying()
        {
            var target = new Vector2(20f, 4f);
            _strike.Launch();
            _strike.Animate(0.01f, target);
            Assert.That(_strike.Flying, Is.EqualTo(1));

            var cog = HeroModel.Strike(3);
            _strike.Use(cog);
            Assert.That(_strike.Art, Is.SameAs(cog));
            Assert.That(_strike.Flying, Is.Zero);
            Assert.That(_scene.transform.Find("Strike crescent 0").GetComponent<SpriteRenderer>().enabled, Is.False);

            _strike.Launch();
            _strike.Animate(HeroStrike.FlightSeconds / 2f, target);
            var flying = _scene.transform.Find("Strike crescent 0");
            if (!flying.GetComponent<SpriteRenderer>().enabled)
                flying = _scene.transform.Find("Strike crescent 1");
            Assert.That(flying.position.y, Is.EqualTo(2f).Within(0.001f), "a cog rolls along the ground the hero stands on");
            _strike.Animate(HeroStrike.FlightSeconds, target);
            Assert.That(_strike.Bursting, Is.EqualTo(1));
        }

        [Test]
        public void EveryStrikeOfTheCatalogHasItsSheets()
        {
            for (var look = 0; look <= 8; look++)
            {
                var art = HeroModel.Strike(look);
                Assert.That(art.Look, Is.EqualTo(look));
                Assert.That(art.Flight.FrameCount, Is.EqualTo(8), "the flight is played over the same 0.15 s for every strike");
                Assert.That(art.Burst.Duration, Is.LessThanOrEqualTo(0.61f));
                Assert.That(art.Slash.FrameCount, Is.EqualTo(HeroModel.Attack.FrameCount));
            }
            Assert.That(HeroModel.Strike(200), Is.SameAs(HeroModel.Strike(0)), "an unknown strike falls back to the void crescent");
            Assert.That(HeroModel.Strike(1).ArcPx, Is.GreaterThan(0f));
            Assert.That(HeroModel.Strike(5).RunsOnGround && HeroModel.Strike(5).BurstsOnGround, Is.True);
        }

        [Test]
        public void EveryStrikeHasAShortFlashADoubleFlashAndTheLongBurst()
        {
            for (var look = 0; look <= 24; look++)
            {
                var art = HeroModel.Strike(look);
                Assert.That(art.HasTiers, Is.True, "look " + look);
                Assert.That(art.BurstOf(1).Duration, Is.InRange(0.2f, 0.25f), "look " + look);
                Assert.That(art.BurstOf(2).Duration, Is.GreaterThan(art.BurstOf(1).Duration).And.LessThan(art.Burst.Duration), "look " + look);
                Assert.That(art.BurstOf(3), Is.SameAs(art.Burst), "look " + look);
                Assert.That(art.BurstOf(9), Is.SameAs(art.Burst));
                Assert.That(art.BurstOf(0), Is.SameAs(art.BurstOf(1)));
            }
        }

        [Test]
        public void TheBurstSheetFollowsTheSwingTierAndKeepsItsSize()
        {
            var art = _strike.Art;
            var target = new Vector2(28f, 5f);
            var burst = _scene.transform.Find("Strike burst 0");

            _strike.Launch(1);
            _strike.Animate(HeroStrike.FlightSeconds, target);
            Assert.That(_strike.Bursting, Is.EqualTo(1));
            _strike.Animate(art.BurstOf(1).Duration, target);
            Assert.That(_strike.Bursting, Is.Zero, "the first tier is the short flash");

            _strike.Launch(3);
            _strike.Animate(HeroStrike.FlightSeconds, target);
            _strike.Animate(art.BurstOf(1).Duration, target);
            Assert.That(_strike.Bursting, Is.EqualTo(1), "the third tier plays the long burst");
            Assert.That(burst.localScale.x, Is.EqualTo(3f), "a tier picks a sheet instead of growing the sprite");
        }

        [Test]
        public void AShortFlashDoesNotCutALongBurstThatIsStillPlaying()
        {
            var art = _strike.Art;
            var target = new Vector2(28f, 5f);

            _strike.Launch(3);
            _strike.Animate(HeroStrike.FlightSeconds, target);
            _strike.Launch(1);
            _strike.Animate(HeroStrike.FlightSeconds, target);
            _strike.Animate(art.BurstOf(1).Duration - HeroStrike.FlightSeconds + 0.01f, target);
            Assert.That(_strike.Bursting, Is.EqualTo(1), "the flash is over, the long burst is not");
            _strike.Launch(1);
            _strike.Animate(HeroStrike.FlightSeconds, target);

            Assert.That(_strike.Bursting, Is.EqualTo(2), "the finished flash gives its place, the long burst goes on");
        }

        [Test]
        public void SkillFamiliesHaveTheirSheets()
        {
            foreach (var id in new[] { "hit", "queue", "auto", "strong", "spark", "wave" })
                Assert.That(HeroModel.SkillSheet(id), Is.Not.Null, id);
            Assert.That(HeroModel.SkillSheet("hit")!.Duration, Is.EqualTo(0.4f).Within(0.02f));
            Assert.That(HeroModel.SkillSheet("queue")!.Duration, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(HeroModel.SkillSheet("auto")!.Duration, Is.EqualTo(0.3f).Within(0.02f));
            Assert.That(HeroModel.SkillSheet("strong")!.Duration, Is.InRange(0.7f, 0.8f));
            Assert.That(HeroModel.SkillSheet("spark")!.Duration, Is.EqualTo(0.2f).Within(0.02f));
            Assert.That(HeroModel.SkillImpactSeconds("strong"), Is.GreaterThan(0f));
            Assert.That(HeroModel.SkillSheet("nothing"), Is.Null);
        }
    }
}
