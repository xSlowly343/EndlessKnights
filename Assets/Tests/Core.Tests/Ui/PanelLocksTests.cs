using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PanelLocksTests
    {
        private const string Valid = @"{ ""version"": 1, ""unlocks"": [ { ""section"": ""party"", ""stage"": 10 }, { ""section"": ""camp"", ""stage"": 150 } ] }";

        [Test]
        public void RealPanelFileLoads()
        {
            var unlocks = PanelUnlocks.Load(TestData.ReadGameDataFile(PanelUnlocks.FileName));

            Assert.That(unlocks.StageFor(PanelSection.Hero), Is.EqualTo(1));
            Assert.That(unlocks.StageFor(PanelSection.Camp), Is.GreaterThan(1));
        }

        [Test]
        public void UnlistedSectionsAreOpenFromTheStart()
        {
            var unlocks = PanelUnlocks.Load(Valid);

            Assert.That(unlocks.IsOpen(PanelSection.Book, 1), Is.True);
            Assert.That(unlocks.IsOpen(PanelSection.Party, 9), Is.False);
            Assert.That(unlocks.IsOpen(PanelSection.Party, 10), Is.True);
        }

        [Test]
        public void UnknownSectionIsRejected()
        {
            var error = Assert.Throws<ContentException>(() => PanelUnlocks.Load(Valid.Replace(@"""camp""", @"""garden""")));

            Assert.That(error!.Message, Does.Contain("garden"));
        }

        [Test]
        public void SectionListedTwiceIsRejected()
        {
            Assert.Throws<ContentException>(() => PanelUnlocks.Load(Valid.Replace(@"""camp""", @"""party""")));
        }

        [Test]
        public void HeroCannotBeLocked()
        {
            Assert.Throws<ContentException>(() => PanelUnlocks.Load(Valid.Replace(@"""camp""", @"""hero""")));
        }

        [Test]
        public void LockedTabIsRefusedAndThePanelStaysAsItWas()
        {
            var navigator = new PanelNavigator { IsUnlocked = section => section != PanelSection.Party };
            var refused = new List<PanelSection>();
            navigator.Refused += refused.Add;
            navigator.SelectTab(PanelSection.Book);

            navigator.SelectTab(PanelSection.Party);

            Assert.That(refused, Is.EqualTo(new[] { PanelSection.Party }));
            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Book));
            Assert.That(navigator.IsOpen, Is.True);
        }

        [Test]
        public void ScrollSkipsLockedTabs()
        {
            var navigator = new PanelNavigator { IsUnlocked = section => section != PanelSection.Items && section != PanelSection.Party };
            navigator.SelectTab(PanelSection.Tree);

            navigator.Scroll(1);

            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Hero));
        }

        [Test]
        public void MarksFollowTheRecordStageAndAnnounceNewSections()
        {
            var marks = new PanelMarks(PanelUnlocks.Load(Valid));
            var state = new GameState { RecordStage = 5 };
            var opened = new List<PanelSection>();
            marks.Unlocked += opened.Add;
            marks.Refresh(state, TestWorld.Data.Ascendancy);

            state.RecordStage = 12;
            var changed = marks.Refresh(state, TestWorld.Data.Ascendancy);

            Assert.That(changed, Is.True);
            Assert.That(marks.IsOpen(PanelSection.Party), Is.True);
            Assert.That(marks.IsOpen(PanelSection.Camp), Is.False);
            Assert.That(opened, Is.EqualTo(new[] { PanelSection.Party }));
        }

        [Test]
        public void FirstRefreshDoesNotAnnounceSectionsThatWereAlreadyOpen()
        {
            var marks = new PanelMarks(PanelUnlocks.Load(Valid));
            var opened = new List<PanelSection>();
            marks.Unlocked += opened.Add;

            marks.Refresh(new GameState { RecordStage = 200 }, TestWorld.Data.Ascendancy);

            Assert.That(opened, Is.Empty);
        }

        [Test]
        public void FreeTreePointsPutADotOnTheTree()
        {
            var marks = new PanelMarks(PanelUnlocks.Load(Valid));
            var state = new GameState();
            state.Tree.PointsEarned = 2;

            marks.Refresh(state, TestWorld.Data.Ascendancy);

            Assert.That(marks.HasDot(PanelSection.Tree), Is.True);
            Assert.That(marks.HasDot(PanelSection.Book), Is.False);
            Assert.That(marks.HasDotAmong(new[] { PanelSection.Portal, PanelSection.Tree }), Is.True);
        }

        [Test]
        public void UnreadMailPutsADotOnTheMail()
        {
            var marks = new PanelMarks(PanelUnlocks.Load(Valid));
            var state = new GameState();
            marks.Refresh(state, TestWorld.Data.Ascendancy);
            Assert.That(marks.HasDot(PanelSection.Mail), Is.False);

            marks.UnreadMail = 1;

            Assert.That(marks.Refresh(state, TestWorld.Data.Ascendancy), Is.True);
            Assert.That(marks.HasDot(PanelSection.Mail), Is.True);
            Assert.That(marks.IsOpen(PanelSection.Mail), Is.True, "mail is open from the first stage");
        }

        [Test]
        public void NoDotWithoutFreePoints()
        {
            var marks = new PanelMarks(PanelUnlocks.Load(Valid));

            marks.Refresh(new GameState(), TestWorld.Data.Ascendancy);

            Assert.That(marks.HasDot(PanelSection.Tree), Is.False);
        }
    }
}
