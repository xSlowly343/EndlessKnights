using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionJointTripTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            loop.State.RecordStage = 100;
            return loop;
        }

        [Test]
        public void TwoCompanionsGoTogetherAndBothAreBusy()
        {
            var loop = Camp();
            Assert.That(loop.Expeditions.SendTogether("camp_dog", "camp_dog", "green_hills", 1), Is.EqualTo(ExpeditionResult.UnknownCompanion));
            Assert.That(loop.Expeditions.SendTogether("camp_dog", "pet_owl", "green_hills", 1), Is.EqualTo(ExpeditionResult.UnknownCompanion));
            Assert.That(loop.Expeditions.SendTogether("camp_dog", "camp_crow", "green_hills", 1), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.Expeditions.Busy("camp_crow"), Is.True);
            Assert.That(loop.Expeditions.Send("camp_crow", "green_hills", 1), Is.EqualTo(ExpeditionResult.CompanionBusy));
            Assert.That(ExpeditionView.Active(loop.Expeditions)[0].Partner, Is.EqualTo("camp_crow"));
        }

        [Test]
        public void TheTripTakesAsLongAsTheSlowerCompanion()
        {
            var loop = Camp();
            loop.Companions.Progress("camp_dog").Level = 30;
            loop.Expeditions.SendTogether("camp_dog", "camp_crow", "green_hills", 8);
            Assert.That(loop.State.Expeditions.Active[0].Seconds, Is.EqualTo(8 * Hour).Within(1e-6));
        }

        [Test]
        public void BothTraitsWorkAndFriendsBringMore()
        {
            var loop = Camp(3);
            loop.Companions.Grant("egg_scarab");
            loop.Expeditions.SendTogether("camp_dog", "egg_scarab", "sand_ruins", 4);
            loop.Expeditions.SendTogether("camp_crow", "egg_griffin", "sand_ruins", 4);
            loop.Expeditions.Advance(4 * Hour);
            var stranger = loop.State.Expeditions.Reports[0];
            Assert.That(stranger.Items[0], Is.EqualTo((long)System.Math.Floor(2 * 4 * 1.5 * 1.25 * 1.6)), "herbalist items, the scarab's own biome, x1.6");
            Assert.That(stranger.Shards, Is.EqualTo((long)System.Math.Floor(3 * 4 * 2 * 1.25 * 1.6)), "digger shards from the scarab");
            var loop2 = Camp(3);
            loop2.Expeditions.SendTogether("camp_dog", "camp_crow", "sand_ruins", 4);
            loop2.Events.Clear();
            loop2.Expeditions.Advance(4 * Hour);
            var friends = loop2.State.Expeditions.Reports[0];
            Assert.That(friends.Shards, Is.EqualTo((long)System.Math.Floor(3 * 4 * 2 * 1.25 * 1.8)));
            Assert.That(loop2.Events.CountOf(GameEventKind.CompanionPairScene), Is.EqualTo(1));
            Assert.That(CompanionView.PairScene(loop2.Companions, "camp_crow", "camp_dog"), Does.Contain("Ворона"));
        }

        [Test]
        public void BothCompanionsGetExperienceBondAndTraitTrips()
        {
            var loop = Camp();
            loop.Expeditions.SendTogether("camp_dog", "camp_crow", "green_hills", 2);
            loop.Expeditions.Advance(2 * Hour);
            Assert.That(loop.Companions.Progress("camp_crow").Experience, Is.EqualTo(20).Within(1e-9));
            Assert.That(loop.Companions.Progress("camp_crow").Bond, Is.EqualTo(1));
            Assert.That(loop.Companions.Progress("camp_crow").TraitTrips["digger"], Is.EqualTo(1));
            Assert.That(loop.Companions.Progress("camp_dog").TraitTrips["herbalist"], Is.EqualTo(1));
        }

        [Test]
        public void TheShoulderTalentAndThePackTalentHelp()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 12;
            loop.Companions.Learn("camp_dog", "shoulder");
            Assert.That(loop.Companions.JointFactor("camp_dog", "camp_crow"), Is.EqualTo(1.8 * 1.1).Within(1e-9));
            dog.Talents.Add("far_call");
            dog.Talents.Add("banner");
            dog.Talents.Add("pack");
            var expedition = new Expedition { Companion = "camp_dog", Partner = "camp_crow", Biome = "green_hills" };
            Assert.That(loop.Companions.StatOf(expedition, TravelStat.Shards), Is.EqualTo(2.25 * 1.8 * 1.1).Within(1e-9));
        }

        [Test]
        public void ThePartnerSurvivesTheSave()
        {
            var loop = Camp();
            loop.Expeditions.SendTogether("camp_dog", "camp_crow", "green_hills", 2);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Expeditions.Active[0].Partner, Is.EqualTo("camp_crow"));
        }
    }
}
