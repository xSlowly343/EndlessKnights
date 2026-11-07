using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionColossusTests
    {
        private static GameLoop Awake(int circle)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            var progress = loop.Companions.Progress("camp_dog");
            progress.Stage = TestWorld.Data.Companions.Stages.Count - 1;
            progress.Circle = circle;
            return loop;
        }

        private static Expedition Trip(GameLoop loop) => loop.Companions.ColossusTrip("camp_dog")!;

        [Test]
        public void TheTrialOpensAfterTheTwelfthCircle()
        {
            var loop = Awake(11);
            Assert.That(loop.Companions.StartColossus("camp_dog"), Is.EqualTo(CompanionResult.Locked));
            loop.Companions.Progress("camp_dog").Circle = 12;
            Assert.That(loop.Companions.StartColossus("camp_dog"), Is.EqualTo(CompanionResult.Done));
            var trip = Trip(loop);
            Assert.That(trip.Seconds, Is.EqualTo(TestWorld.Data.Companions.Colossus.Hours * ExpeditionSystem.SecondsPerHour));
            Assert.That(trip.Biome, Is.EqualTo(TestWorld.Data.Companions.Companion("camp_dog")!.Biome));
            Assert.That(loop.Companions.StartColossus("camp_dog"), Is.EqualTo(CompanionResult.Busy));
        }

        [Test]
        public void ThreeLettersArriveOverTheTripAndShapeTheForm()
        {
            var loop = Awake(12);
            var companions = loop.Companions;
            companions.StartColossus("camp_dog");
            Assert.That(companions.AnswerColossus("camp_dog", "call"), Is.EqualTo(CompanionResult.TooSoon));
            Trip(loop).Elapsed = Trip(loop).Planned / 4;
            Assert.That(companions.ColossusLetters("camp_dog"), Is.EqualTo(1));
            Assert.That(companions.AnswerColossus("camp_dog", "dance"), Is.EqualTo(CompanionResult.UnknownTrait));
            Assert.That(companions.AnswerColossus("camp_dog", "call"), Is.EqualTo(CompanionResult.Done));
            Assert.That(companions.AnswerColossus("camp_dog", "call"), Is.EqualTo(CompanionResult.TooSoon));
            Trip(loop).Elapsed = Trip(loop).Planned * 0.8;
            Assert.That(companions.ColossusLetters("camp_dog"), Is.EqualTo(2));
            companions.AnswerColossus("camp_dog", "retreat");
            companions.AnswerColossus("camp_dog", "call");
            loop.Expeditions.Advance(Trip(loop).Planned);
            Assert.That(companions.CircleProgress("camp_dog"), Is.EqualTo(1));
            Assert.That(companions.IsColossus("camp_dog"), Is.False, "the form comes with the thirteenth circle");
            loop.State.Companions.Essence = 1000;
            loop.Camp.Add(TestWorld.Data.Companions.Bloom, 10);
            Assert.That(companions.AdvanceCircle("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(companions.IsColossus("camp_dog"), Is.True);
            Assert.That(companions.ColossusFormOf("camp_dog")!.Id, Is.EqualTo("herald"));
            Assert.That(companions.Statuettes(), Is.EqualTo(new[] { "camp_dog" }));
        }

        [Test]
        public void SilenceCountsAsStaying()
        {
            var loop = Awake(12);
            loop.Companions.StartColossus("camp_dog");
            loop.Expeditions.Advance(Trip(loop).Planned);
            Assert.That(loop.State.Companions.Progress["camp_dog"].ColossusForm, Is.EqualTo("steadfast"));
        }

        [Test]
        public void TheFormRaisesEveryNumberAndOpensTheBracelet()
        {
            var loop = Awake(12);
            var factor = loop.Companions.TraitFactor("camp_dog");
            Assert.That(loop.CompanionGear.SlotOpen("camp_dog", CompanionGearSlot.Bracelet), Is.False);
            loop.Companions.Progress("camp_dog").Circle = 13;
            var circle = TestWorld.Data.Companions.Circles.Circle(13)!;
            Assert.That(loop.Companions.TraitFactor("camp_dog"), Is.EqualTo(factor * (1 + circle.Traits) * 1.1).Within(1e-9));
            Assert.That(loop.CompanionGear.SlotOpen("camp_dog", CompanionGearSlot.Bracelet), Is.True);
            Assert.That(loop.Companions.InHall("camp_dog"), Is.False);
            loop.Companions.Progress("camp_dog").Circle = 15;
            Assert.That(loop.Companions.InHall("camp_dog"), Is.True);
        }

        [Test]
        public void TheFoldCannotSkipTheTrial()
        {
            var loop = Awake(12);
            loop.Companions.Grant("heart_fold");
            loop.Companions.Progress("heart_fold").Circle = 10;
            loop.Companions.StartColossus("camp_dog");
            Assert.That(loop.CompanionAbilities.Use("heart_fold"), Is.EqualTo(CompanionResult.NothingToDo));
            Assert.That(loop.CompanionAbilities.Use("heart_fold", 0), Is.EqualTo(CompanionResult.NothingToDo));
        }

        [Test]
        public void TheTrialSurvivesASave()
        {
            var loop = Awake(12);
            loop.Companions.StartColossus("camp_dog");
            Trip(loop).Elapsed = Trip(loop).Planned / 2;
            loop.Companions.AnswerColossus("camp_dog", "retreat");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Expeditions.Active[0].Colossus, Is.True);
            Assert.That(copy.Companions.Progress["camp_dog"].ColossusAnswers, Is.EqualTo(new[] { "retreat" }));
        }
    }
}
