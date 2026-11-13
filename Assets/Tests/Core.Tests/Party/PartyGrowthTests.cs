using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyGrowthTests
    {
        private static PartyGrowthRules Rules => TestWorld.Data.Party.Growth;

        [Test]
        public void TheLevelCurveMatchesTheServer()
        {
            Assert.That(Rules.MaxLevel, Is.EqualTo(100));
            Assert.That(Rules.Need(1), Is.EqualTo(250));
            Assert.That(Rules.Need(4), Is.EqualTo(430));
            Assert.That(Rules.TotalFor(5), Is.EqualTo(1360));
            Assert.That(Rules.LevelOf(1359).Level, Is.EqualTo(4));
            var five = Rules.LevelOf(1360);
            Assert.That(five.Level, Is.EqualTo(5));
            Assert.That(five.Into, Is.Zero);
            Assert.That(five.Need, Is.EqualTo(490));
            Assert.That(Rules.LevelOf(1e12).Max, Is.True);
            Assert.That(Rules.LevelOf(1e12).Level, Is.EqualTo(100));
        }

        [Test]
        public void EveryFifthLevelOpensSomethingAndTheMascotComesAtFive()
        {
            for (var level = 5; level <= Rules.MaxLevel; level += 5)
                Assert.That(Rules.Unlocks.Any(u => u.Level == level), Is.True, "level " + level);
            Assert.That(Rules.Opened(PartyUnlockKind.Mascot, 4), Is.False);
            Assert.That(Rules.Opened(PartyUnlockKind.Mascot, 5), Is.True);
            Assert.That(Rules.CountOpened(PartyUnlockKind.BannerSlot, 25), Is.EqualTo(2));
            Assert.That(Rules.NextUnlockLevel(1), Is.EqualTo(5));
            Assert.That(Rules.NextUnlockLevel(100), Is.Zero);
        }

        [Test]
        public void OnlySecondsWithInputAreReported()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            for (var i = 0; i < 25; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            for (var i = 0; i < 50; i++)
                loop.Tick();
            Assert.That(loop.PartyActivity.TakeActiveSeconds(), Is.EqualTo(2));
            Assert.That(loop.PartyActivity.TakeActiveSeconds(), Is.Zero, "the half second waits for the next report");
            Assert.That(loop.PartyActivity.PendingSeconds, Is.Zero);
        }

        [Test]
        public void TheViewShowsLevelProgressAndTheNextOpening()
        {
            var view = PartyLevelView.Of(Rules, 1360 + 100);
            Assert.That(view.Level, Is.EqualTo(5));
            Assert.That(view.Title, Does.Contain("5"));
            Assert.That(view.ProgressText, Does.Contain("100").And.Contain("490"));
            Assert.That(view.NextLevel, Is.EqualTo(10));
            Assert.That(view.NextUnlocks.Count, Is.EqualTo(2));
            Assert.That(view.NextText, Does.Contain("10"));
            Assert.That(view.NextUnlocks.All(text => !text.StartsWith("party.")), Is.True);
            var top = PartyLevelView.Of(Rules, 1e12);
            Assert.That(top.Max, Is.True);
            Assert.That(top.NextText, Is.Empty);
            Assert.That(top.ProgressText, Does.Not.StartWith("party."));
        }

        [Test]
        public void AnUnknownOpeningIsRefused()
        {
            var text = Game.Core.Tests.Content.TestData.ReadGameDataFile(PartyRules.FileName);
            Assert.Throws<ContentException>(() => PartyRules.Load(text.Replace("\"kind\": \"mascot\"", "\"kind\": \"dragon\"")));
            Assert.Throws<ContentException>(() => PartyRules.Load(text.Replace("{ \"level\": 5, \"kind\": \"mascot\" }", "{ \"level\": 500, \"kind\": \"mascot\" }")));
        }
    }
}
