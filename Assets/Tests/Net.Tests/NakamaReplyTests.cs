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
        public void APartyViewIsReadFromTheServerReply()
        {
            var json = NakamaJson.Parse("{\"id\":\"p1\",\"code\":\"K7MQ2X\",\"access\":\"open\",\"title\":\"night_shift\",\"language\":\"ru\",\"wanted\":\"buffer\",\"leader\":\"u1\",\"maxMembers\":4,\"matchId\":null,"
                + "\"members\":[{\"userId\":\"u1\",\"name\":\"Ann\",\"steamId\":\"76561198000000000\",\"leader\":true},{\"userId\":\"u2\",\"name\":\"Bob\",\"steamId\":\"\",\"leader\":false}]}");

            var view = PartyView.From(json)!;

            Assert.That(view.Code, Is.EqualTo("K7MQ2X"));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.MatchId, Is.EqualTo(string.Empty));
            Assert.That(view.Members.Count, Is.EqualTo(2));
            Assert.That(view.Members[0].Leader, Is.True);
            Assert.That(view.Members[0].SteamId, Is.EqualTo("76561198000000000"));
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
