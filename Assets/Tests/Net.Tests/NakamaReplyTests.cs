using System;
using System.Text;
using Game.Net.Nakama;
using NUnit.Framework;

namespace Game.Net.Tests
{
    public class NakamaReplyTests
    {
        private static string Token(string claims)
        {
            static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return Part("{\"alg\":\"HS256\"}") + "." + Part(claims) + ".signature";
        }

        [Test]
        public void ASessionReadsTheUserAndExpiryFromTheToken()
        {
            var session = NakamaSession.FromTokens(Token("{\"uid\":\"3e02-user\",\"exp\":1800000000,\"usn\":\"bot\"}"), "refresh");

            Assert.That(session.UserId, Is.EqualTo("3e02-user"));
            Assert.That(session.ExpiresAtUnix, Is.EqualTo(1800000000));
            Assert.That(session.RefreshToken, Is.EqualTo("refresh"));
            Assert.That(session.ExpiresWithin(1799999990, 60), Is.True);
            Assert.That(session.ExpiresWithin(1700000000, 60), Is.False);
        }

        [Test]
        public void ABrokenTokenIsRefused() => Assert.Throws<NakamaException>(() => NakamaSession.FromTokens("not-a-jwt", ""));

        [TestCase("Error: not in a party at partyConnectRpc (index.js:600:15(12))", "not in a party")]
        [TestCase("party is full", "party is full")]
        [TestCase("Error: unknown code", "unknown code")]
        public void ServerErrorsAreShownWithoutTheScriptStack(string raw, string clean) => Assert.That(NakamaClient.Clean(raw), Is.EqualTo(clean));

        [Test]
        public void ARiftBoardAndItsRewardsAreReadFromTheServerReply()
        {
            var board = RiftBoardView.From(NakamaJson.Parse("{\"riftId\":1793102400,\"endUnixMs\":1793188800000,\"settled\":false,\"parties\":14,"
                + "\"top\":[{\"place\":1,\"title\":\"night_shift\",\"depth\":40,\"members\":4,\"yours\":false},{\"place\":2,\"title\":\"loud_keys\",\"depth\":31,\"members\":2,\"yours\":true}],"
                + "\"own\":{\"place\":2,\"depth\":31,\"reward\":{\"upTo\":3,\"shards\":200,\"uncutGems\":2,\"ingots\":4}}}"))!;
            var reward = new RiftRewardView(NakamaJson.Parse("{\"riftId\":1793102400,\"place\":2,\"of\":14,\"depth\":31,\"title\":\"loud_keys\",\"shards\":200,\"uncutGems\":2,\"ingots\":4,\"claimed\":true}"));

            Assert.That(board.Parties, Is.EqualTo(14));
            Assert.That(board.Top[1].Yours, Is.True);
            Assert.That(board.Top[0].Depth, Is.EqualTo(40));
            Assert.That(board.OwnPlace, Is.EqualTo(2));
            Assert.That(board.EndUnixMs, Is.EqualTo(1793188800000));
            Assert.That(reward.Place, Is.EqualTo(2));
            Assert.That(reward.Ingots, Is.EqualTo(4));
            Assert.That(reward.UncutGems, Is.EqualTo(2));
        }

        [Test]
        public void AChronicleIsReadFromTheServerReply()
        {
            var json = NakamaJson.Parse("{\"partyId\":\"p1\",\"banner\":2,\"titles\":[\"full_orchestra\"],\"stats\":{\"bossesTogether\":120,\"hoursTogether\":3.5,\"fullOrchestra\":1},"
                + "\"feats\":[{\"id\":\"old_guard\",\"kind\":\"bossesTogether\",\"tracked\":true,\"levels\":[100,1000,10000],\"level\":1,\"at\":1792760000000,\"progress\":120},"
                + "{\"id\":\"night_shift\",\"kind\":\"nightBosses\",\"tracked\":false,\"levels\":[20],\"level\":0,\"at\":0,\"progress\":0}],"
                + "\"records\":[{\"at\":1792760000000,\"kind\":\"feat\",\"name\":\"\",\"feat\":\"old_guard\",\"level\":1},{\"at\":1792700000000,\"kind\":\"joined\",\"name\":\"Bob\",\"feat\":\"\",\"level\":0}]}");

            var chronicle = PartyChronicleView.From(json)!;

            Assert.That(chronicle.Banner, Is.EqualTo(2));
            Assert.That(chronicle.Titles, Is.EqualTo(new[] { "full_orchestra" }));
            Assert.That(chronicle.BossesTogether, Is.EqualTo(120));
            Assert.That(chronicle.HoursTogether, Is.EqualTo(3.5));
            Assert.That(chronicle.Feats[0].Levels, Is.EqualTo(new[] { 100.0, 1000, 10000 }));
            Assert.That(chronicle.Feats[0].Level, Is.EqualTo(1));
            Assert.That(chronicle.Feats[1].Tracked, Is.False);
            Assert.That(chronicle.Records[1].Name, Is.EqualTo("Bob"));
            Assert.That(chronicle.Records[0].AtUnixMs, Is.EqualTo(1792760000000));
        }

        [Test]
        public void APartyViewIsReadFromTheServerReply()
        {
            var json = NakamaJson.Parse("{\"id\":\"p1\",\"code\":\"K7MQ2X\",\"access\":\"open\",\"title\":\"night_shift\",\"language\":\"ru\",\"wanted\":\"buffer\",\"leader\":\"u1\",\"maxMembers\":4,\"matchId\":null,"
                + "\"members\":[{\"userId\":\"u1\",\"name\":\"Ann\",\"steamId\":\"76561198000000000\",\"leader\":true,\"slot\":1},{\"userId\":\"u2\",\"name\":\"Bob\",\"steamId\":\"\",\"leader\":false}]}");

            var view = PartyView.From(json)!;

            Assert.That(view.Code, Is.EqualTo("K7MQ2X"));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.MatchId, Is.EqualTo(string.Empty));
            Assert.That(view.Members.Count, Is.EqualTo(2));
            Assert.That(view.Members[0].Leader, Is.True);
            Assert.That(view.Members[0].SteamId, Is.EqualTo("76561198000000000"));
            Assert.That(view.Members[0].Slot, Is.EqualTo(1));
            Assert.That(view.Members[1].Slot, Is.EqualTo(-1));
            Assert.That(view.UserIdAtSlot(1), Is.EqualTo("u1"));
            Assert.That(view.UserIdAtSlot(0), Is.Null);
            Assert.That(view.UserIdAtSlot(-1), Is.Null);
            Assert.That(PartyView.From(NakamaJson.Parse("null")), Is.Null);
        }

        [Test]
        public void ACardKeepsWhatTheSearchShows()
        {
            var card = new PartyCard(NakamaJson.Parse("{\"id\":\"p2\",\"title\":\"lunch_raid\",\"language\":\"en\",\"wanted\":\"any\",\"members\":3,\"maxMembers\":4,\"activeNow\":true,\"usuallyOnline\":\"day\",\"wantsYou\":true}"));

            Assert.That(card.Members, Is.EqualTo(3));
            Assert.That(card.ActiveNow, Is.True);
            Assert.That(card.UsuallyOnline, Is.EqualTo("day"));
        }

        [Test]
        public void ObjectsAreWrittenWithEscapedStrings() =>
            Assert.That(NakamaJson.Object(("code", NakamaJson.Quote("a\"b")), ("n", "4")), Is.EqualTo("{\"code\":\"a\\\"b\",\"n\":4}"));
    }
}
