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
        public void ARiftBoardAndARewardLetterAreReadFromTheServerReply()
        {
            var board = RiftBoardView.From(NakamaJson.Parse("{\"riftId\":1793102400,\"endUnixMs\":1793188800000,\"settled\":false,\"parties\":14,"
                + "\"top\":[{\"place\":1,\"title\":\"night_shift\",\"depth\":40,\"members\":4,\"yours\":false},{\"place\":2,\"title\":\"loud_keys\",\"depth\":31,\"members\":2,\"yours\":true}],"
                + "\"own\":{\"place\":2,\"depth\":31,\"reward\":{\"upTo\":3,\"shards\":200,\"uncutGems\":2,\"ingots\":4}}}"))!;
            var letter = MailDirectory.Letter(NakamaJson.Parse("{\"id\":\"rift-1793102400\",\"kind\":\"rift\",\"at\":1793189100000,\"place\":2,\"of\":14,\"depth\":31,\"title\":\"loud_keys\","
                + "\"reward\":{\"shards\":200,\"uncutGems\":2,\"ingots\":4,\"friendship\":20},\"claimed\":false,\"claimedAt\":0}"));

            Assert.That(board.Parties, Is.EqualTo(14));
            Assert.That(board.Top[1].Yours, Is.True);
            Assert.That(board.Top[0].Depth, Is.EqualTo(40));
            Assert.That(board.OwnPlace, Is.EqualTo(2));
            Assert.That(board.EndUnixMs, Is.EqualTo(1793188800000));
            Assert.That(letter.Id, Is.EqualTo("rift-1793102400"));
            Assert.That(letter.Kind, Is.EqualTo("rift"));
            Assert.That(letter.Place, Is.EqualTo(2));
            Assert.That(letter.Reward.Ingots, Is.EqualTo(4));
            Assert.That(letter.Reward.UncutGems, Is.EqualTo(2));
            Assert.That(letter.Reward.Friendship, Is.EqualTo(20));
            Assert.That(letter.Claimed, Is.False);
        }

        [Test]
        public void TheClanRaidIsReadWithLayersPlacesAndLastWeek()
        {
            var raid = RaidView.From(NakamaJson.Parse("{\"boss\":\"devourer\",\"week\":2964,\"endsAt\":1793566800000,\"members\":27,\"killedAt\":0,\"left\":0.58,"
                + "\"layers\":[{\"id\":\"shield\",\"hp\":27000,\"left\":0},{\"id\":\"armor\",\"hp\":40500,\"left\":13770}],"
                + "\"own\":{\"share\":0.021,\"place\":9,\"players\":27,\"seals\":41},"
                + "\"top\":[{\"place\":1,\"userId\":\"ann\",\"name\":\"Ann\",\"share\":0.12}],"
                + "\"last\":{\"week\":2963,\"killed\":true,\"removed\":1,\"seals\":55,\"place\":4,\"players\":22}}"));
            var fresh = RaidView.From(NakamaJson.Parse("{\"boss\":\"devourer\",\"layers\":[],\"own\":{\"share\":0,\"place\":0,\"players\":0,\"seals\":0},\"top\":[],\"last\":null}"));

            Assert.That(raid.Boss, Is.EqualTo("devourer"));
            Assert.That(raid.Members, Is.EqualTo(27));
            Assert.That(raid.Left, Is.EqualTo(0.58));
            Assert.That(raid.Layers[1].Left, Is.EqualTo(13770));
            Assert.That(raid.OwnPlace, Is.EqualTo(9));
            Assert.That(raid.OwnSeals, Is.EqualTo(41));
            Assert.That(raid.Top[0].Name, Is.EqualTo("Ann"));
            Assert.That(raid.HasLast && raid.LastKilled, Is.True);
            Assert.That(raid.LastSeals, Is.EqualTo(55));
            Assert.That(fresh.HasLast, Is.False);
            Assert.That(raid.Daily, Is.Null);
            var daily = ClanDailyView.From(NakamaJson.Parse("{\"day\":20755,\"endsAt\":1793307600000,\"challenges\":[{\"id\":\"keys\",\"counter\":\"keys\",\"target\":120000,"
                + "\"progress\":82000,\"done\":0,\"yours\":3100,\"seals\":10},{\"id\":\"mouse\",\"counter\":\"mouseMeters\",\"target\":12000,\"progress\":12000,\"done\":1793280000000,\"yours\":0,\"seals\":10}]}"));
            Assert.That(daily.Challenges[0].Progress, Is.EqualTo(82000));
            Assert.That(daily.Challenges[0].Done, Is.False);
            Assert.That(daily.Challenges[1].Done, Is.True);
            Assert.That(daily.Challenges[1].Counter, Is.EqualTo("mouseMeters"));
        }

        [Test]
        public void TheMentorStatusAndPartyFlagsAreRead()
        {
            var mentor = MentorView.From(NakamaJson.Parse("{\"role\":\"mentor\",\"canMentor\":true,\"ready\":true,\"studentUntil\":1793873289000,"
                + "\"partner\":{\"userId\":\"kid\",\"name\":\"Лёша\"},\"seconds\":13200,\"level\":1,\"marks\":[\"str_12\"],\"perks\":[],\"coopPoints\":0,\"graduates\":2}"));
            var student = MentorView.From(NakamaJson.Parse("{\"role\":\"student\",\"canMentor\":false,\"ready\":false,\"studentUntil\":1793873289000,\"partner\":null,\"seconds\":0,\"level\":0,\"marks\":[],\"perks\":[]}"));
            var party = PartyView.From(NakamaJson.Parse("{\"id\":\"p\",\"members\":[{\"userId\":\"kid\",\"student\":true,\"mentor\":false,\"slot\":1},{\"userId\":\"vet\",\"student\":false,\"mentor\":true,\"slot\":0}]}"))!;
            var card = new PartyCard(NakamaJson.Parse("{\"id\":\"p\",\"hasMentor\":true,\"hasStudent\":false}"));

            Assert.That(mentor.IsMentor, Is.True);
            Assert.That(mentor.PartnerName, Is.EqualTo("Лёша"));
            Assert.That(mentor.Seconds, Is.EqualTo(13200));
            Assert.That(mentor.Graduates, Is.EqualTo(2));
            Assert.That(student.IsStudent && !student.HasPartner, Is.True);
            Assert.That(party.Members[0].Student && party.Members[1].Mentor, Is.True);
            Assert.That(card.HasMentor && !card.HasStudent, Is.True);
        }

        [Test]
        public void TheClanTreeIsReadWithTheTreasuryDonorsAndLog()
        {
            var tree = ClanTreeView.From(NakamaJson.Parse("{\"treasury\":1240,\"taken\":[\"common_hearth\",\"war_drums\"],\"yours\":300,"
                + "\"donors\":[{\"userId\":\"bob\",\"name\":\"Bob\",\"amount\":900}],\"log\":[{\"at\":1793188800000,\"name\":\"Ann\",\"kind\":\"bought\",\"amount\":600,\"node\":\"war_drums\"}]}"));

            Assert.That(tree.Treasury, Is.EqualTo(1240));
            Assert.That(tree.Taken, Is.EqualTo(new[] { "common_hearth", "war_drums" }));
            Assert.That(tree.Yours, Is.EqualTo(300));
            Assert.That(tree.Donors[0].Amount, Is.EqualTo(900));
            Assert.That(tree.Log[0].Node, Is.EqualTo("war_drums"));
            Assert.That(ClanTreeView.DonatePayload(50), Is.EqualTo("{\"amount\":50}"));
        }

        [Test]
        public void TheCoopWalletIsReadWithTodayAndTheLedger()
        {
            var wallet = CoopWalletView.From(NakamaJson.Parse("{\"balances\":{\"friendship\":46,\"seals\":320},"
                + "\"today\":[{\"source\":\"boss_together\",\"currency\":\"friendship\",\"earned\":7,\"cap\":20}],"
                + "\"ledger\":[{\"at\":1793188800000,\"source\":\"gift\",\"currency\":\"friendship\",\"amount\":-3},{\"at\":1793102400000,\"source\":\"boss_together\",\"currency\":\"friendship\",\"amount\":7}]}"));

            Assert.That(wallet.Friendship, Is.EqualTo(46));
            Assert.That(wallet.Seals, Is.EqualTo(320));
            Assert.That(wallet.Today[0].Earned, Is.EqualTo(7));
            Assert.That(wallet.Today[0].Cap, Is.EqualTo(20));
            Assert.That(wallet.Ledger[0].Amount, Is.EqualTo(-3));
            Assert.That(wallet.Ledger[1].Source, Is.EqualTo("boss_together"));
        }

        [Test]
        public void AClanIsReadFromTheServerReplyWithRolesAndLastSeen()
        {
            var clan = ClanView.From(NakamaJson.Parse("{\"id\":\"c1\",\"code\":\"K7MQ2X\",\"access\":\"open\",\"adjective\":\"lilac\",\"noun\":\"lanterns\",\"language\":\"ru\","
                + "\"leader\":\"ann\",\"maxMembers\":30,\"maxOfficers\":4,\"activeNow\":2,\"members\":[{\"userId\":\"ann\",\"name\":\"Ann\",\"steamId\":\"76561198000000000\",\"role\":\"leader\",\"seen\":1793188800000},"
                + "{\"userId\":\"bob\",\"name\":\"Bob\",\"steamId\":\"\",\"role\":\"officer\",\"seen\":1793188700000}]}"))!;
            var card = new ClanCard(NakamaJson.Parse("{\"id\":\"c2\",\"adjective\":\"golden\",\"noun\":\"owls\",\"language\":\"en\",\"members\":27,\"maxMembers\":30,\"activeNow\":14}"));

            Assert.That(clan.IsOpen, Is.True);
            Assert.That(clan.Adjective + " " + clan.Noun, Is.EqualTo("lilac lanterns"));
            Assert.That(clan.MaxMembers, Is.EqualTo(30));
            Assert.That(clan.ActiveNow, Is.EqualTo(2));
            Assert.That(clan.RoleOf("bob"), Is.EqualTo("officer"));
            Assert.That(clan.RoleOf("eve"), Is.Empty);
            Assert.That(clan.Members[0].SeenUnixMs, Is.EqualTo(1793188800000));
            Assert.That(card.Members, Is.EqualTo(27));
            Assert.That(card.ActiveNow, Is.EqualTo(14));
            Assert.That(ClanView.From(NakamaJson.Field(NakamaJson.Parse("{\"clan\":null}"), "clan")), Is.Null);
        }

        [Test]
        public void AGhostIsReadFromTheServerReplyWithItsMarks()
        {
            var synergy = Game.Core.Content.Synergy.SynergyRules.Load(System.IO.File.ReadAllText(DataFile("synergy.json")));
            var json = NakamaJson.Parse("{\"userId\":\"u1\",\"name\":\"Ann\",\"heroClass\":8,\"shareRate\":0.4,\"marks\":[\"curse\",\"stun\"],\"activity\":0.75}");

            var ghost = GhostDirectory.Snapshot(json, synergy);

            Assert.That(ghost.Name, Is.EqualTo("Ann"));
            Assert.That(ghost.HeroClass, Is.EqualTo(Game.Core.Sim.HeroClass.Voodoo));
            Assert.That(ghost.ShareRate, Is.EqualTo(0.4));
            Assert.That(ghost.Activity, Is.EqualTo(0.75));
            Assert.That(ghost.Marks, Is.EqualTo(synergy.MaskOf(new[] { "curse", "stun" })));
        }

        private static string DataFile(string name)
        {
            var directory = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (directory != null && !System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "Assets", "StreamingAssets", "Data")))
                directory = directory.Parent;
            return System.IO.Path.Combine(directory!.FullName, "Assets", "StreamingAssets", "Data", name);
        }

        [Test]
        public void AChronicleIsReadFromTheServerReply()
        {
            var json = NakamaJson.Parse("{\"partyId\":\"p1\",\"banner\":2,\"titles\":[\"full_orchestra\"],\"stats\":{\"bossesTogether\":120,\"hoursTogether\":3.5,\"fullOrchestra\":1,\"riftDepth\":64,\"mentorHours\":2.5},"
                + "\"feats\":[{\"id\":\"old_guard\",\"kind\":\"bossesTogether\",\"tracked\":true,\"levels\":[100,1000,10000],\"level\":1,\"at\":1792760000000,\"progress\":120},"
                + "{\"id\":\"night_shift\",\"kind\":\"nightBosses\",\"tracked\":false,\"levels\":[20],\"level\":0,\"at\":0,\"progress\":0}],"
                + "\"records\":[{\"at\":1792760000000,\"kind\":\"feat\",\"name\":\"\",\"feat\":\"old_guard\",\"level\":1},{\"at\":1792700000000,\"kind\":\"joined\",\"name\":\"Bob\",\"feat\":\"\",\"level\":0}]}");

            var chronicle = PartyChronicleView.From(json)!;

            Assert.That(chronicle.Banner, Is.EqualTo(2));
            Assert.That(chronicle.Titles, Is.EqualTo(new[] { "full_orchestra" }));
            Assert.That(chronicle.BossesTogether, Is.EqualTo(120));
            Assert.That(chronicle.HoursTogether, Is.EqualTo(3.5));
            Assert.That(chronicle.RiftDepth, Is.EqualTo(64));
            Assert.That(chronicle.MentorHours, Is.EqualTo(2.5));
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
