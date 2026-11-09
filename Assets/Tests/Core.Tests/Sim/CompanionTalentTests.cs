using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionTalentTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void ThreeBranchesOfFourAndAPointEveryThreeLevels()
        {
            var talents = TestWorld.Data.Companions.Talents;
            Assert.That(talents.Nodes.Count, Is.EqualTo(12));
            var loop = Camp();
            loop.Companions.Progress("camp_dog").Level = 30;
            Assert.That(loop.Companions.TalentPoints("camp_dog"), Is.EqualTo(10));
        }

        [Test]
        public void NodesAreLearnedInOrderWithFreePoints()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Learn("camp_dog", "light_step"), Is.EqualTo(CompanionResult.NoPoints));
            loop.Companions.Progress("camp_dog").Level = 6;
            Assert.That(loop.Companions.Learn("camp_dog", "full_bags"), Is.EqualTo(CompanionResult.NeedsPrevious));
            Assert.That(loop.Companions.Learn("camp_dog", "light_step"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Learn("camp_dog", "light_step"), Is.EqualTo(CompanionResult.Learned));
            Assert.That(loop.Companions.Learn("camp_dog", "full_bags"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Learn("camp_dog", "warm_corner"), Is.EqualTo(CompanionResult.NoPoints));
            Assert.That(loop.Expeditions.DurationFactor("camp_dog"), Is.EqualTo(0.95).Within(1e-9), "2% from level 5 and 3% from the talent");
        }

        [Test]
        public void TheLongRoadNeedsTheYoungster()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 12;
            dog.Talents.Add("light_step");
            dog.Talents.Add("full_bags");
            dog.Talents.Add("sharp_eye");
            Assert.That(loop.Companions.Learn("camp_dog", "long_road"), Is.EqualTo(CompanionResult.LowLevel));
            dog.Stage = 1;
            Assert.That(loop.Companions.Learn("camp_dog", "long_road"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 16), Is.EqualTo(ExpeditionResult.Done));
        }

        [Test]
        public void FullBagsAddAnItem()
        {
            var loop = Camp(2);
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 6;
            loop.Companions.Learn("camp_dog", "light_step");
            loop.Companions.Learn("camp_dog", "full_bags");
            loop.Expeditions.Send("camp_dog", "sand_ruins", 4);
            loop.State.RecordStage = 100;
            loop.Expeditions.Send("camp_dog", "sand_ruins", 4);
            loop.Expeditions.Advance(4 * Hour);
            var report = loop.State.Expeditions.Reports[0];
            Assert.That(report.Items[0], Is.EqualTo((long)System.Math.Floor(2 * 4 * 0.95 * 1.5 * 1.1) + 1));
        }

        [Test]
        public void TheHomeBranchBoostsHomeTraitsAndForages()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 12;
            dog.Stage = 1;
            loop.Companions.ChooseTrait("camp_dog", "fisher");
            loop.Companions.StayHome("camp_dog");
            loop.Companions.Learn("camp_dog", "warm_corner");
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.BiteFaster], Is.EqualTo(0.055).Within(1e-9));
            loop.Companions.Learn("camp_dog", "home_lessons");
            loop.Companions.Learn("camp_dog", "second_helping");
            loop.Companions.Learn("camp_dog", "house_master");
            loop.Companions.Advance(2 * Hour);
            Assert.That(loop.Camp.Count("herb_knockbloom"), Is.EqualTo(1));
            Assert.That(loop.Camp.Count("fish_hills_roach"), Is.EqualTo(1));
        }

        [Test]
        public void TheResetIsFreeOnceAWeekThenCostsShards()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 3;
            loop.Companions.Learn("camp_dog", "light_step");
            Assert.That(loop.Companions.ResetTalents("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(dog.Talents, Is.Empty);
            loop.Companions.Learn("camp_dog", "light_step");
            Assert.That(loop.Companions.ResetTalents("camp_dog"), Is.EqualTo(CompanionResult.NoShards));
            loop.State.Drops.Shards = 30;
            Assert.That(loop.Companions.ResetTalents("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(0));
            loop.SetLocalDay(107);
            Assert.That(loop.Companions.FreeResetReady("camp_dog"), Is.True);
        }

        [Test]
        public void TalentsSurviveTheSave()
        {
            var loop = Camp();
            loop.Companions.Progress("camp_dog").Level = 3;
            loop.Companions.Learn("camp_dog", "shoulder");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Talents, Does.Contain("shoulder"));
            Assert.That(copy.Companions.Progress["camp_dog"].FreeResetDay, Is.EqualTo(-1));
        }
    }
}
