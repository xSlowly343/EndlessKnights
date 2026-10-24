using System;
using System.Linq;
using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PartyRosterTests
    {
        private GameState _state = null!;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
            _state = new GameState { Class = HeroClass.Viking, Stage = 37 };
        }

        [Test]
        public void SoloShowsYouAndThreeFreeSlots()
        {
            var rows = PartyRoster.Solo(_state);

            Assert.That(rows.Count, Is.EqualTo(PartyRules.MaxSlots));
            Assert.That(rows[0].IsSelf, Is.True);
            Assert.That(rows[0].Name, Is.EqualTo("Вы"));
            Assert.That(rows[0].Details, Does.EndWith("· этап 37"));
            Assert.That(rows.Skip(1).All(row => row.IsFree), Is.True);
            Assert.That(PartyRoster.Headline(rows), Is.EqualTo("В отряде 1 из 4"));
        }

        [Test]
        public void LinkedPartyListsOtherPlayersWithClassAndStatus()
        {
            var roster = new RosterMessage
            {
                Player0 = 11, Class0 = (byte)HeroClass.Viking, Presence0 = (byte)PartyPresence.Fighting,
                Player1 = 22, Class1 = (byte)HeroClass.Metalhead, Presence1 = (byte)PartyPresence.Resting,
                Player3 = 44, Class3 = (byte)HeroClass.Voodoo, Presence3 = (byte)PartyPresence.Offline,
            };

            var rows = PartyRoster.From(_state, PartyLink.Linked, roster, 0);

            Assert.That(rows.Count, Is.EqualTo(PartyRules.MaxSlots));
            Assert.That(rows[0].IsSelf, Is.True);
            Assert.That(rows[1].Name, Is.EqualTo("Игрок 2"));
            Assert.That(rows[1].Details, Is.EqualTo(HeroSummary.ClassName(HeroClass.Metalhead)));
            Assert.That(PartyRoster.Status(rows[1].Presence), Is.EqualTo("отдыхает"));
            Assert.That(PartyRoster.Status(rows[2].Presence), Is.EqualTo("нет связи"));
            Assert.That(rows[3].IsFree, Is.True);
            Assert.That(PartyRoster.Members(rows), Is.EqualTo(3));
        }

        [Test]
        public void WithoutALinkTheRosterIsSolo()
        {
            var roster = new RosterMessage { Player0 = 11, Player1 = 22 };

            var rows = PartyRoster.From(_state, PartyLink.Joining, roster, 0);

            Assert.That(PartyRoster.Members(rows), Is.EqualTo(1));
        }

        [Test]
        public void EveryPresenceHasAStatus()
        {
            foreach (PartyPresence presence in Enum.GetValues(typeof(PartyPresence)))
                Assert.That(Texts.Has("party.status." + presence.ToString().ToLowerInvariant()), Is.True, presence.ToString());
        }
    }
}
