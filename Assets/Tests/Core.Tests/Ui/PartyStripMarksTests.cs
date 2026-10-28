using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PartyStripMarksTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void ColoursFollowThePanelRows()
        {
            var roster = new RosterMessage { Player0 = 1, Player1 = 2, Player3 = 4 };

            Assert.That(PartyStripMarks.ColorIndexOf(roster, 1, 1), Is.EqualTo(0));
            Assert.That(PartyStripMarks.ColorIndexOf(roster, 1, 0), Is.EqualTo(1));
            Assert.That(PartyStripMarks.ColorIndexOf(roster, 1, 3), Is.EqualTo(2));
            Assert.That(PartyStripMarks.ColorIndexOf(roster, 1, 2), Is.EqualTo(PartyStripMarks.NoColor));
            Assert.That(PartyStripMarks.ColorIndexOf(roster, PartyEncounter.NoSlot, 0), Is.EqualTo(PartyStripMarks.NoColor));
            Assert.That(PartyStripMarks.ColorIndexOf(roster, 1, PartyEncounter.NoSlot), Is.EqualTo(PartyStripMarks.NoColor));
        }

        [Test]
        public void BossSharesNameYouAndTheOthersLikeThePanel()
        {
            var roster = new RosterMessage { Player0 = 1, Player2 = 3, Player3 = 4 };
            var kill = new EnemyKilledMessage { Share0 = 0.25, Share2 = 0.41, Share3 = 0.34, Rewarded = 0b1101 };
            var shares = new BossShare[PartyRules.MaxSlots];

            var count = PartyStripMarks.FillBossShares(kill, roster, 2, shares);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(shares[0].Label, Is.EqualTo("Игрок 2 25%"));
            Assert.That(shares[0].ColorIndex, Is.EqualTo(1));
            Assert.That(shares[1].Label, Is.EqualTo("Вы 41%"));
            Assert.That(shares[1].ColorIndex, Is.EqualTo(0));
            Assert.That(shares[2].Label, Is.EqualTo("Игрок 3 34%"));
            Assert.That(shares[2].Share, Is.EqualTo(0.34));
            Assert.That(PartyStripMarks.BossTitle(true), Does.StartWith("Босс повержен"));
            Assert.That(PartyStripMarks.BossTitle(false), Does.Contain("без награды"));
        }

        [Test]
        public void TheBossTitleNamesTheLoopAndTheSavedSeconds()
        {
            var kill = new EnemyKilledMessage { Share0 = 1, Rewarded = 1, Loop = 0.18f, SavedSeconds = 6.4f };

            Assert.That(PartyStripMarks.BossTitle(kill, 0), Is.EqualTo("Босс повержен · вклад отряда · петля 18% · таймер +6 с"));
            Assert.That(PartyStripMarks.BossTitle(new EnemyKilledMessage { Share0 = 1 }, 0), Is.EqualTo("Босс повержен · вам без награды: не было ввода"));
        }
    }
}
