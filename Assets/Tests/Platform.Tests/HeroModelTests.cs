using Game.Presentation.Battle;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class HeroModelTests
    {
        [Test]
        public void SeekerSheetsAreSlicedIntoFramesAtTheFeet()
        {
            Assert.That(HeroModel.Idle.FrameCount, Is.EqualTo(24));
            Assert.That(HeroModel.Attack.FrameCount, Is.EqualTo(8));
            var idle = HeroModel.Idle.Frame(0);
            Assert.That(idle.rect.width, Is.EqualTo(30f));
            Assert.That(idle.pivot.x, Is.EqualTo(15f).Within(0.01f));
            Assert.That(idle.pivot.y, Is.EqualTo(2f).Within(0.01f));
            Assert.That(HeroModel.Attack.Frame(7).rect.x, Is.EqualTo(630f));
        }

        [Test]
        public void IdleLoopsAndAttackHoldsItsLastFrame()
        {
            Assert.That(HeroModel.Idle.Duration, Is.EqualTo(2.4f).Within(0.001f));
            Assert.That(HeroModel.Idle.FrameAt(2.45f), Is.Zero);
            Assert.That(HeroModel.Attack.Duration, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(HeroModel.Attack.FrameAt(0.09f), Is.EqualTo(2));
            Assert.That(HeroModel.Attack.FrameAt(5f), Is.EqualTo(7));
        }

        [Test]
        public void SlashIsItsOwnLayerAndLandsOnTheHitFrame()
        {
            Assert.That(HeroModel.Slash.FrameCount, Is.EqualTo(HeroModel.Attack.FrameCount));
            Assert.That(HeroModel.Slash.Duration, Is.EqualTo(HeroModel.Attack.Duration).Within(0.001f));
            Assert.That(HeroModel.Slash.Frame(3).pivot, Is.EqualTo(HeroModel.Attack.Frame(3).pivot));
            Assert.That(HeroModel.StrikeSeconds, Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(HeroModel.Attack.FrameAt(HeroModel.StrikeSeconds), Is.EqualTo(HeroModel.AttackHitFrame));
        }

        [Test]
        public void StrikeEffectsAreSlicedAtTheirPivots()
        {
            Assert.That(HeroModel.Crescent.FrameCount, Is.EqualTo(8));
            Assert.That(HeroModel.Crescent.Frame(0).rect.size, Is.EqualTo(new UnityEngine.Vector2(87f, 42f)));
            Assert.That(HeroModel.Crescent.Frame(0).pivot.x, Is.EqualTo(72f).Within(0.01f));
            Assert.That(HeroModel.Burst.FrameCount, Is.EqualTo(17));
            Assert.That(HeroModel.Burst.Frame(16).rect.x, Is.EqualTo(1280f));
            Assert.That(HeroModel.Burst.Frame(0).pivot.y, Is.EqualTo(27f).Within(0.01f));
        }
    }
}
