using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SocialRulesTests
    {
        private static SocialRules Real => SocialRules.Load(TestData.ReadGameDataFile(SocialRules.FileName));

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [Test]
        public void TheGameFileHoldsThreeChannelsTwoTicketsAndTwoRoles()
        {
            var rules = Real;
            Assert.That(rules.Channels.Count, Is.EqualTo(3));
            Assert.That(rules.Channel(SocialRules.World).PerMinute, Is.LessThan(rules.Channel(SocialRules.Party).PerMinute));
            Assert.That(rules.TicketPrice(SocialRules.PartyRenameTicket), Is.GreaterThan(0));
            Assert.That(rules.TicketPrice(SocialRules.ClanRenameTicket), Is.GreaterThan(rules.TicketPrice(SocialRules.PartyRenameTicket)));
            Assert.That(rules.IsRole("developer") && rules.IsRole("moderator"), Is.True);
            Assert.That(rules.IsRole("king"), Is.False);
        }

        [Test]
        public void EveryIdHasItsTextInBothLanguages()
        {
            var rules = Real;
            foreach (var language in new[] { "ru", "en" })
            {
                Texts.UseLanguage(language);
                foreach (var channel in rules.Channels)
                    Assert.That(Texts.Has("chat.channel." + channel.Id), Is.True, channel.Id);
                foreach (var role in rules.Roles)
                    Assert.That(Texts.Has("chat.role." + role.Id), Is.True, role.Id);
                foreach (var reason in rules.ReportReasons)
                    Assert.That(Texts.Has("chat.report." + reason), Is.True, reason);
                foreach (var ticket in rules.Tickets)
                {
                    Assert.That(Texts.Has("ticket." + ticket.Id), Is.True, ticket.Id);
                    Assert.That(Texts.Has("wallet.source.ticket_" + ticket.Id), Is.True, ticket.Id);
                }
            }
        }

        [TestCase("Ночные совы", SocialNameProblem.None)]
        [TestCase("  Ночные   совы  ", SocialNameProblem.None)]
        [TestCase("O'Neil-7 owls", SocialNameProblem.None)]
        [TestCase("ab", SocialNameProblem.TooShort)]
        [TestCase("abcdefghijklmnopqrstuvwxyz", SocialNameProblem.TooLong)]
        [TestCase("owls!!!", SocialNameProblem.BadSymbols)]
        [TestCase("<b>owls</b>", SocialNameProblem.BadSymbols)]
        [TestCase("-owls", SocialNameProblem.BadSymbols)]
        [TestCase("owls--owls", SocialNameProblem.BadSymbols)]
        [TestCase("Fuck owls", SocialNameProblem.Banned)]
        [TestCase("F u c k owls", SocialNameProblem.Banned)]
        [TestCase("sh1t happens", SocialNameProblem.Banned)]
        [TestCase("Xyй вам", SocialNameProblem.Banned)]
        public void ANameIsCheckedTheSameWayAsOnTheServer(string name, SocialNameProblem expected)
        {
            Assert.That(Real.CheckName(name), Is.EqualTo(expected));
        }

        [Test]
        public void ChatTextIsTidiedAndMeasured()
        {
            var rules = Real;
            Assert.That(SocialRules.Tidy("  привет,\n  отряд\t"), Is.EqualTo("привет, отряд"));
            Assert.That(rules.FitsChat("   "), Is.False);
            Assert.That(rules.FitsChat("hi"), Is.True);
            Assert.That(rules.FitsChat(new string('x', rules.ChatMax + 1)), Is.False);
        }

        [Test]
        public void AnOwnNameReplacesTheReadyOneEverywhere()
        {
            Assert.That(PartyCardText.Title("night_shift"), Is.EqualTo(Texts.Get("party.title.night_shift")));
            Assert.That(PartyCardText.Title("=Ночные совы"), Is.EqualTo("Ночные совы"));
            Assert.That(PartyCardText.Title("night_shift", "Совы"), Is.EqualTo("Совы"));
            Assert.That(PartyCardText.Title("night_shift", string.Empty), Is.EqualTo(Texts.Get("party.title.night_shift")));
            Assert.That(PartySearchRow.From("p", "=Ночные совы", "ru", "any", 1, 4, false, null, false).Title, Is.EqualTo("Ночные совы"));
            Assert.That(ClanText.Name("lilac", "lanterns", "Орден Кромки"), Is.EqualTo("Орден Кромки"));
            Assert.That(ClanText.Name("lilac", "lanterns", string.Empty), Is.EqualTo(ClanText.Name("lilac", "lanterns")));
            Assert.That(ClanText.Tag("lilac", "lanterns", "Орден Кромки"), Is.EqualTo("ОК"));
            Assert.That(ClanText.Tag("lilac", "lanterns", "Совы"), Is.EqualTo("СО"));
        }

        [Test]
        public void TextsExplainNamesTicketsAndMutes()
        {
            var rules = Real;
            Assert.That(ChatText.NameProblem(SocialNameProblem.None, rules), Is.Empty);
            Assert.That(ChatText.NameProblem(SocialNameProblem.TooShort, rules), Does.Contain(rules.NameMin.ToString()));
            Assert.That(ChatText.RenameHint(true, 0), Is.EqualTo(Texts.Get("name.hint.free")));
            Assert.That(ChatText.RenameHint(false, 2), Does.Contain("2"));
            Assert.That(ChatText.RenameHint(false, 0), Is.EqualTo(Texts.Get("name.hint.noTicket")));
            Assert.That(ChatText.Muted(90 * 60000, 0), Is.EqualTo(Texts.Format("chat.muted.minutes", 90)));
            Assert.That(ChatText.Muted(5 * 3600000, 0), Is.EqualTo(Texts.Format("chat.muted.hours", 5)));
            Assert.That(ChatText.Line(new ChatLine(1, "a", "Ann", "developer", "hi", 0)), Is.EqualTo("[" + Texts.Get("chat.role.developer") + "] Ann: hi"));
            Assert.That(ChatText.Line(new ChatLine(1, "a", "Ann", string.Empty, "hi", 0)), Is.EqualTo("Ann: hi"));
            Assert.That(ChatText.Failed("too fast"), Does.Contain(Texts.Get("chat.reason.tooFast")));
        }

        [Test]
        public void ABrokenFileIsRejectedWithItsPlace()
        {
            var json = TestData.ReadGameDataFile(SocialRules.FileName);
            Assert.Throws<ContentException>(() => SocialRules.Load(json.Replace("\"id\": \"world\"", "\"id\": \"galaxy\"")));
            Assert.Throws<ContentException>(() => SocialRules.Load(json.Replace("\"id\": \"clan_rename\"", "\"id\": \"party_rename\"")));
            Assert.Throws<ContentException>(() => SocialRules.Load(json.Replace("\"fuck\"", "\"FU\"")));
        }

        [Test]
        public void TheChatLogKeepsNewLinesDropsRemovedOnesAndCountsUnread()
        {
            var log = new ChatLog(SocialRules.Party, 3);
            log.Receive("room", 2, new List<ChatLine> { Line(1, "ann"), Line(2, "bob") }, new List<long>(), false, string.Empty, 0);
            Assert.That(log.Unread, Is.EqualTo(2));
            log.MarkRead();
            log.Receive("room", 5, new List<ChatLine> { Line(2, "bob"), Line(3, "ann"), Line(4, "eve"), Line(5, "bob") }, new List<long> { 4 }, true, "moderator", 77);
            Assert.That(Seqs(log), Is.EqualTo(new long[] { 2, 3, 5 }));
            Assert.That(log.Unread, Is.EqualTo(2));
            Assert.That(log.LastSeq, Is.EqualTo(5));
            Assert.That(log.CanModerate && log.Role == "moderator", Is.True);
            Assert.That(log.IsMuted(76), Is.True);
            Assert.That(log.IsMuted(77), Is.False);
            log.Hide("bob");
            Assert.That(Seqs(log), Is.EqualTo(new long[] { 3 }));
            log.Receive("other", 1, new List<ChatLine> { Line(1, "cat") }, new List<long>(), false, string.Empty, 0);
            Assert.That(Seqs(log), Is.EqualTo(new long[] { 1 }));
            Assert.That(log.CanDelete(log.Lines[0], "cat"), Is.True);
            Assert.That(log.CanDelete(log.Lines[0], "ann"), Is.False);
        }

        private static ChatLine Line(long seq, string from) => new ChatLine(seq, from, from, string.Empty, "text " + seq, seq * 1000);

        private static long[] Seqs(ChatLog log)
        {
            var seqs = new long[log.Lines.Count];
            for (var i = 0; i < seqs.Length; i++)
                seqs[i] = log.Lines[i].Seq;
            return seqs;
        }
    }
}
