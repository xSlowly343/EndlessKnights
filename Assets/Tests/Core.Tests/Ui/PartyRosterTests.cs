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
            Assert.That(rows[1].Details, Is.EqualTo(HeroSummary.ClassName(HeroClass.Metalhead) + " · запас отдыха 0 мин"));
            Assert.That(PartyRoster.Status(rows[1].Presence), Is.EqualTo("отдыхает"));
            Assert.That(PartyRoster.Status(rows[2].Presence), Is.EqualTo("нет связи"));
            Assert.That(rows[3].IsFree, Is.True);
            Assert.That(PartyRoster.Members(rows), Is.EqualTo(3));
            Assert.That(rows.Select(row => row.Slot), Is.EqualTo(new[] { 0, 1, 3, PartyEncounter.NoSlot }));
            Assert.That(rows.Select(row => row.CanBeReported), Is.EqualTo(new[] { false, true, true, false }));
        }

        [Test]
        public void AFreshReactionIsShownOnItsRow()
        {
            var roster = new RosterMessage { Player0 = 11, Player2 = 33, Class2 = (byte)HeroClass.Oracle };

            var rows = PartyRoster.From(_state, PartyLink.Linked, roster, 0, null, true, slot => slot == 2 ? "Привет!" : string.Empty);

            Assert.That(rows[0].Reaction, Is.Empty);
            Assert.That(rows[1].Slot, Is.EqualTo(2));
            Assert.That(rows[1].Reaction, Is.EqualTo("Привет!"));
            Assert.That(rows[2].Reaction, Is.Empty);
        }

        [Test]
        public void ReactionTextsComeFromThePartyRules()
        {
            var rules = TestWorld.Data.Party.Reactions;

            Assert.That(rules.Ids.Select(PartyReactions.Label), Has.None.StartsWith("party."));
            Assert.That(PartyReactions.Label(rules, 0), Is.EqualTo("Привет!"));
            Assert.That(PartyReactions.Label(rules, PartyClient.NoReaction), Is.Empty);
            Assert.That(PartyReactions.Said("Игрок 2", "Привет!"), Is.EqualTo("Игрок 2: «Привет!»"));
            Assert.That(PartyReactions.Wait(rules), Is.EqualTo("Реакции — не чаще раза в 3 с"));
            Texts.UseLanguage("en");
            Assert.That(rules.Ids.Select(PartyReactions.Label), Has.None.StartsWith("party."));
            Texts.UseLanguage("ru");
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

        [Test]
        public void ContributionRestBankAndAMissedRewardAreShown()
        {
            var roster = new RosterMessage
            {
                Player0 = 11, Class0 = (byte)HeroClass.Viking, Presence0 = (byte)PartyPresence.Fighting,
                Player1 = 22, Class1 = (byte)HeroClass.Gravedigger, Presence1 = (byte)PartyPresence.Resting, RestBank1 = 25 * 60 + 40,
            };
            double Share(int slot) => slot == 0 ? 0.625 : 0.375;

            var rows = PartyRoster.From(_state, PartyLink.Linked, roster, 0, Share, false);

            Assert.That(rows[0].Details, Is.EqualTo(HeroSummary.ClassName(HeroClass.Viking) + " · этап 37 · вклад 63% · без награды: нет ввода и запаса отдыха"));
            Assert.That(rows[1].Details, Is.EqualTo(HeroSummary.ClassName(HeroClass.Gravedigger) + " · вклад 38% · запас отдыха 25 мин"));
        }
    }
}
