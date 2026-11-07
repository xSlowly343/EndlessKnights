using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionFormTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void TheAdultChoosesATrailOrAHomeForm()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            Assert.That(loop.Companions.ChooseForm("camp_dog", "trail"), Is.EqualTo(CompanionResult.LowLevel));
            dog.Stage = 2;
            var before = loop.Expeditions.DurationFactor("camp_dog");
            var items = loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items);
            Assert.That(loop.Companions.ChooseForm("camp_dog", "trail"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Expeditions.DurationFactor("camp_dog"), Is.EqualTo(before * 0.85).Within(1e-9));
            Assert.That(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items), Is.EqualTo(items * 1.15).Within(1e-9));
            Assert.That(loop.Companions.ChooseForm("camp_dog", "trail"), Is.EqualTo(CompanionResult.Repeat));
            Assert.That(loop.Companions.ChooseForm("camp_dog", "home"), Is.EqualTo(CompanionResult.NoSouls), "a change costs souls");
            loop.Companions.AddSouls(100);
            Assert.That(loop.Companions.ChooseForm("camp_dog", "home"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Companions.Souls, Is.Zero);
            Assert.That(loop.Companions.Stars("camp_dog"), Is.EqualTo(3 + 2), "each form is a star of the album");
        }

        [Test]
        public void TheHomeFormStrengthensHomeTraits()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 20;
            dog.Stage = 2;
            loop.Companions.ChooseTrait("camp_dog", "fisher");
            loop.Companions.StayHome("camp_dog");
            var plain = loop.State.CompanionPerks[(int)MuseumPerk.BiteFaster];
            loop.Companions.ChooseForm("camp_dog", "home");
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.BiteFaster], Is.EqualTo(plain * 1.15).Within(1e-9));
        }

        [Test]
        public void TheUndersideFormNeedsTheAwakenedWithThreeStars()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 3;
            Assert.That(loop.Companions.ChooseForm("camp_dog", "underside"), Is.EqualTo(CompanionResult.Locked));
            dog.Stars = 3;
            Assert.That(loop.Companions.ChooseForm("camp_dog", "underside"), Is.EqualTo(CompanionResult.Done));
            var copy = Game.Core.Save.GameStateJson.Read(new ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Form, Is.EqualTo("underside"));
            Assert.That(copy.Companions.Progress["camp_dog"].Forms, Is.EqualTo(new[] { "underside" }));
        }
    }
}
