using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
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

        [Test]
        public void AnAllyInPeacefulModeShowsWhatTheyAreDoingAndWhere()
        {
            Texts.UseLanguage("ru");
            var fishing = new PartyPastime(AllyActivity.Fishing, StripPlace.Hideout);
            var roster = new RosterMessage
            {
                Player0 = 11, Presence0 = (byte)PartyPresence.Peaceful, Pastime0 = fishing.Packed,
                Player1 = 22,
                Player2 = 33, Presence2 = (byte)PartyPresence.Resting, Pastime2 = fishing.Packed
            };

            StripAllies.Fill(roster, 1, _ => string.Empty, _allies);

            Assert.That(_allies[0].Pastime.Activity, Is.EqualTo(AllyActivity.Fishing));
            Assert.That(AllyPastimeView.Shows(_allies[0]), Is.True);
            Assert.That(AllyPastimeView.Icon(_allies[0].Pastime), Is.EqualTo("pastime_fishing"));
            Assert.That(AllyPastimeView.PlaceIcon(_allies[0].Pastime), Is.EqualTo("place_hideout"));
            Assert.That(AllyPastimeView.Caption(TestWorld.Real.Peaceful, _allies[0].Pastime), Is.EqualTo("Пристанище · Рыбалка"));
            Assert.That(AllyPastimeView.Shows(_allies[1]), Is.False, "a resting fighter shows no pastime even if one came over the wire");
        }

        [Test]
        public void PeacefulAlliesAreTranslucentAndOfflineOnesAreFadedWithTheirOwnIcon()
        {
            var roster = new RosterMessage
            {
                Player0 = 11, Presence0 = (byte)PartyPresence.Peaceful, Pastime0 = new PartyPastime(AllyActivity.Forge, StripPlace.Hideout).Packed,
                Player1 = 22,
                Player2 = 33, Presence2 = (byte)PartyPresence.Offline,
                Player3 = 44
            };

            StripAllies.Fill(roster, 1, _ => string.Empty, _allies);

            Assert.That(_allies[0].Translucent, Is.True);
            Assert.That(_allies[0].Faded, Is.False);
            Assert.That(AllyPastimeView.IconOf(_allies[0]), Is.EqualTo("pastime_forge"));
            Assert.That(AllyPastimeView.PlaceIconOf(_allies[0]), Is.EqualTo("place_hideout"));
            Assert.That(_allies[1].Faded, Is.True);
            Assert.That(_allies[1].Translucent, Is.False);
            Assert.That(AllyPastimeView.IconOf(_allies[1]), Is.EqualTo(AllyPastimeView.OfflineIcon));
            Assert.That(AllyPastimeView.PlaceIconOf(_allies[1]), Is.Empty);
            Assert.That(AllyPastimeView.KeyOf(_allies[1]), Is.Not.EqualTo(AllyPastimeView.KeyOf(_allies[0])));
            Assert.That(_allies[2].Translucent || _allies[2].Faded, Is.False, "online and fighting is drawn as usual");
            Assert.That(AllyPastimeView.IconOf(_allies[2]), Is.Empty);
        }

        [Test]
        public void TheHostPassesAPastimeOnOnlyWhileTheAllyIsPeaceful()
        {
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 1);
            encounter.Join(1);
            encounter.Join(2);
            var garden = new PartyPastime(AllyActivity.Garden, StripPlace.Town).Packed;

            Assert.That(encounter.SetPastime(1, garden), Is.True);
            Assert.That(encounter.SetPastime(1, garden), Is.False, "the same pastime is no news for the roster");
            Assert.That(RosterMessage.Of(encounter, 1).Pastime(1).Known, Is.False);
            encounter.SetPeaceful(1, true);
            var shown = RosterMessage.Of(encounter, 2).Pastime(1);
            Assert.That(shown.Activity, Is.EqualTo(AllyActivity.Garden));
            Assert.That(shown.Place, Is.EqualTo(StripPlace.Town));
        }

        [Test]
        public void APastimePacksIntoOneByteAndEveryPeacefulActivityHasAnIcon()
        {
            foreach (var activity in TestWorld.Real.Peaceful.Activities)
            {
                var mark = PartyPastime.ActivityOf(activity.Id);
                Assert.That(mark, Is.Not.EqualTo(AllyActivity.None), activity.Id + " has an icon over the ally");
                Assert.That(PartyPastime.IdOf(mark), Is.EqualTo(activity.Id));
                foreach (var place in new[] { StripPlace.Hideout, StripPlace.ClanCamp, StripPlace.Town })
                {
                    var unpacked = PartyPastime.Unpack(new PartyPastime(mark, place).Packed);
                    Assert.That(unpacked.Activity, Is.EqualTo(mark));
                    Assert.That(unpacked.Place, Is.EqualTo(place));
                }
            }
            Assert.That(PartyPastime.Unpack(0).Known, Is.False);
            Assert.That(PartyPastime.Unpack(0xFF).Activity, Is.EqualTo(AllyActivity.None), "a byte from a newer client is not misread");
            Assert.That(PartyPastime.Unpack(0x01).Place, Is.EqualTo(StripPlace.Hideout), "a fighting place in the byte reads as the hideout");
        }

        [TestCase(PartyPresence.Fighting, "")]
        [TestCase(PartyPresence.Resting, "z")]
        [TestCase(PartyPresence.Peaceful, "~")]
        [TestCase(PartyPresence.Offline, "!")]
        public void EveryStatusHasAMark(PartyPresence presence, string mark) => Assert.That(StripAllies.StatusMark(presence), Is.EqualTo(mark));
    }
}
