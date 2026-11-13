using System.Collections.Generic;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionVisitTests
    {
        [Test]
        public void AVisitingCompanionIsBusyAndComesBackWithGiftsExperienceAndBond()
        {
            Texts.UseLanguage("ru");
            var loop = TestWorld.Loop(new FakeInputSource());
            var companions = loop.Companions;
            var companion = TestWorld.Data.Expeditions.Companions[0].Id;
            Assert.That(companions.CanVisit("dragon"), Is.EqualTo(CompanionResult.UnknownCompanion));
            Assert.That(CompanionVisitView.Status(companions, "Rex", 0), Is.EqualTo(Texts.Get("visit.none")));
            Assert.That(companions.StartVisit(companion, "Bob", 4 * 3600000L), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Expeditions.Busy(companion), Is.True, "a visiting companion does not go on trips");
            Assert.That(companions.CanVisit(companion), Is.EqualTo(CompanionResult.Busy));
            Assert.That(CompanionVisitView.Status(companions, "Rex", 3600000L), Does.Contain("Bob").And.Contain("3 ч 0 мин"));
            Assert.That(companions.VisitBack(3600000L), Is.False);
            Assert.That(companions.VisitBack(4 * 3600000L), Is.True);
            Assert.That(CompanionVisitView.Status(companions, "Rex", 4 * 3600000L), Is.EqualTo(Texts.Format("visit.ready", "Rex", "Bob")));

            var experience = companions.Progress(companion).Experience;
            var level = companions.LevelOf(companion);
            var bond = companions.Progress(companion).Bond;
            Assert.That(companions.FinishVisit(new Dictionary<string, long> { ["fish_hills_roach"] = 1 }), Is.EqualTo(companion));
            Assert.That(loop.Camp.Count("fish_hills_roach"), Is.EqualTo(1));
            Assert.That(companions.LevelOf(companion) > level || companions.Progress(companion).Experience > experience, Is.True, "the trip taught the companion something");
            Assert.That(companions.Progress(companion).Bond, Is.GreaterThan(bond));
            Assert.That(loop.Expeditions.Busy(companion), Is.False);
            Assert.That(companions.FinishVisit(new Dictionary<string, long>()), Is.Empty, "nothing to finish twice");
        }

        [Test]
        public void TheVisitSurvivesTheSave()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var companion = TestWorld.Data.Expeditions.Companions[0].Id;
            loop.Companions.StartVisit(companion, "Bob", 123456);
            var copy = Game.Core.Save.GameStateJson.Read(new Game.Core.Content.ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Visiting, Is.EqualTo(companion));
            Assert.That(copy.Companions.VisitHost, Is.EqualTo("Bob"));
            Assert.That(copy.Companions.VisitEndsUnixMs, Is.EqualTo(123456));
        }
    }
}
