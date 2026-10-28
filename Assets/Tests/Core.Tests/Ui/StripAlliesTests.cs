using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class StripAlliesTests
    {
        private readonly StripAlly[] _allies = new StripAlly[StripAllies.MaxShown];

        [Test]
        public void OthersAreShownInRosterOrderWithThePanelColours()
        {
            var roster = new RosterMessage
            {
                Player0 = 11, Presence0 = (byte)PartyPresence.Resting,
                Player1 = 22,
                Player3 = 44, Presence3 = (byte)PartyPresence.Offline,
            };

            var count = StripAllies.Fill(roster, 1, slot => slot == 3 ? "Привет!" : string.Empty, _allies);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(_allies[0].Slot, Is.EqualTo(0));
            Assert.That(_allies[0].ColorIndex, Is.EqualTo(1));
            Assert.That(_allies[0].Presence, Is.EqualTo(PartyPresence.Resting));
            Assert.That(_allies[0].Swings, Is.False);
            Assert.That(_allies[1].Slot, Is.EqualTo(3));
            Assert.That(_allies[1].ColorIndex, Is.EqualTo(2));
            Assert.That(_allies[1].Faded, Is.True);
            Assert.That(_allies[1].Reaction, Is.EqualTo("Привет!"));
        }

        [Test]
        public void AFullPartyShowsThreeAllies()
        {
            var roster = new RosterMessage { Player0 = 1, Player1 = 2, Player2 = 3, Player3 = 4 };

            Assert.That(StripAllies.Fill(roster, 2, _ => string.Empty, _allies), Is.EqualTo(3));
            Assert.That(_allies[2].Slot, Is.EqualTo(3));
            Assert.That(_allies[2].Swings, Is.True);
        }

        [Test]
        public void WithoutYourOwnSlotNobodyIsShown()
        {
            var roster = new RosterMessage { Player0 = 1, Player1 = 2 };

            Assert.That(StripAllies.Fill(roster, PartyEncounter.NoSlot, _ => string.Empty, _allies), Is.Zero);
        }

        [TestCase(PartyPresence.Fighting, "")]
        [TestCase(PartyPresence.Resting, "z")]
        [TestCase(PartyPresence.Peaceful, "~")]
        [TestCase(PartyPresence.Offline, "!")]
        public void EveryStatusHasAMark(PartyPresence presence, string mark) => Assert.That(StripAllies.StatusMark(presence), Is.EqualTo(mark));
    }
}
