using System.Linq;
using System;
using System.Text;
using Game.Net.Nakama;
using Game.Core.Mail;
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
        public void ACouponReplyGivesItsStatus()
        {
            Assert.That(CouponDirectory.Status("{\"status\":\"done\",\"letter\":{\"id\":\"coupon:A\"}}"), Is.EqualTo("done"));
            Assert.That(CouponDirectory.Status("{\"status\":\"used\"}"), Is.EqualTo("used"));
            Assert.That(CouponDirectory.Status("{}"), Is.Empty);
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
                + "\"own\":{\"place\":2,\"depth\":31,\"reward\":{\"upTo\":3,\"shards\":200,\"ingots\":4}}}"))!;
            var letter = MailDirectory.Letter(NakamaJson.Parse("{\"id\":\"rift-1793102400\",\"kind\":\"rift\",\"at\":1793189100000,\"place\":2,\"of\":14,\"depth\":31,\"title\":\"loud_keys\","
                + "\"reward\":{\"shards\":200,\"ingots\":4,\"friendship\":20},\"claimed\":false,\"claimedAt\":0}"));

            Assert.That(board.Parties, Is.EqualTo(14));
            Assert.That(board.Top[1].Yours, Is.True);
            Assert.That(board.Top[0].Depth, Is.EqualTo(40));
            Assert.That(board.OwnPlace, Is.EqualTo(2));
            Assert.That(board.EndUnixMs, Is.EqualTo(1793188800000));
            Assert.That(letter.Id, Is.EqualTo("rift-1793102400"));
            Assert.That(letter.Kind, Is.EqualTo("rift"));
            Assert.That(letter.Place, Is.EqualTo(2));
            Assert.That(letter.Reward.Ingots, Is.EqualTo(4));
            Assert.That(letter.Reward.Shards, Is.EqualTo(200));
            Assert.That(letter.Reward.Friendship, Is.EqualTo(20));
            Assert.That(letter.Claimed, Is.False);
        }

        [Test]
        public void CallsForHelpAndTheAnswerAreRead()
        {
            var invites = HelpDirectory.Invites(NakamaJson.Parse("{\"invites\":[{\"from\":\"ann-id\",\"name\":\"Ann\",\"until\":1793189160000}]}"));
            var answer = HelpDirectory.Answer(NakamaJson.Parse("{\"matchId\":\"abc.nakama1\",\"seconds\":30}"));

            Assert.That(invites, Has.Count.EqualTo(1));
            Assert.That(invites[0].From, Is.EqualTo("ann-id"));
            Assert.That(invites[0].Name, Is.EqualTo("Ann"));
            Assert.That(invites[0].UntilUnixMs, Is.EqualTo(1793189160000));
            Assert.That(answer.MatchId, Is.EqualTo("abc.nakama1"));
            Assert.That(answer.Seconds, Is.EqualTo(30));
            Assert.Throws<NakamaException>(() => HelpDirectory.Answer(NakamaJson.Parse("{}")));
        }

        [Test]
        public void TheClanRaidIsReadWithLayersPlacesAndLastWeek()
        {
            var raid = RaidView.From(NakamaJson.Parse("{\"boss\":\"devourer\",\"week\":2964,\"endsAt\":1793566800000,\"members\":27,\"killedAt\":0,\"left\":0.58,\"coven\":0.25,"
                + "\"layers\":[{\"id\":\"shield\",\"hp\":27000,\"left\":0},{\"id\":\"armor\",\"hp\":40500,\"left\":13770}],"
                + "\"own\":{\"share\":0.021,\"place\":9,\"players\":27,\"seals\":41},"
                + "\"top\":[{\"place\":1,\"userId\":\"ann\",\"name\":\"Ann\",\"share\":0.12}],"
                + "\"last\":{\"week\":2963,\"killed\":true,\"removed\":1,\"seals\":55,\"place\":4,\"players\":22}}"));
            var fresh = RaidView.From(NakamaJson.Parse("{\"boss\":\"devourer\",\"layers\":[],\"own\":{\"share\":0,\"place\":0,\"players\":0,\"seals\":0},\"top\":[],\"last\":null}"));

            Assert.That(raid.Boss, Is.EqualTo("devourer"));
            Assert.That(raid.Members, Is.EqualTo(27));
            Assert.That(raid.Left, Is.EqualTo(0.58));
            Assert.That(raid.Coven, Is.EqualTo(0.25));
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
        public void ClanRequestsAndApplicationsAreRead()
        {
            var requests = ClanRequestsView.From(NakamaJson.Parse("{\"clanId\":\"c1\",\"days\":7,\"requests\":[{\"userId\":\"bob\",\"name\":\"Bob\",\"at\":1793188800000,\"until\":1793793600000}]}"));
            var card = new ClanCard(NakamaJson.Parse("{\"id\":\"c1\",\"byRequest\":true}"));
            var application = new ClanApplicationRow(NakamaJson.Parse("{\"clanId\":\"c1\",\"adjective\":\"lilac\",\"noun\":\"lanterns\",\"members\":12,\"at\":1,\"until\":1793793600000}"));

            Assert.That(requests.Days, Is.EqualTo(7));
            Assert.That(requests.Requests[0].Name, Is.EqualTo("Bob"));
            Assert.That(requests.Requests[0].UntilUnixMs, Is.EqualTo(1793793600000));
            Assert.That(card.ByRequest, Is.True);
            Assert.That(application.Noun, Is.EqualTo("lanterns"));
            Assert.That(application.Members, Is.EqualTo(12));
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
        public void CoopHelpPointsAreReadWithEverySource()
        {
            var help = CoopHelpView.From(NakamaJson.Parse("{\"points\":3,\"max\":5,\"sources\":[{\"source\":\"mentor\",\"points\":1,\"max\":2},"
                + "{\"source\":\"bosses\",\"points\":1,\"max\":2,\"progress\":57,\"next\":200},{\"source\":\"rift\",\"points\":1,\"max\":2,\"stages\":[100]},"
                + "{\"source\":\"wanderers\",\"points\":0,\"max\":2,\"thisWeek\":false}]}"));

            Assert.That(help.Points, Is.EqualTo(3));
            Assert.That(help.Max, Is.EqualTo(5));
            Assert.That(help.Sources.Count, Is.EqualTo(4));
            Assert.That(help.Sources[1].Source, Is.EqualTo("bosses"));
            Assert.That(help.Sources[1].Progress, Is.EqualTo(57));
            Assert.That(help.Sources[1].Next, Is.EqualTo(200));
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
        public void APartyQuestClaimSendsTheBossesAndReadsTheGrantAndWallet()
        {
            Assert.That(PartyQuestClaim.Payload(5), Is.EqualTo("{\"bosses\":5}"));
            var result = PartyQuestClaimResult.From(NakamaJson.Parse("{\"granted\":3,\"wallet\":{\"balances\":{\"friendship\":49,\"seals\":0},\"today\":[],\"ledger\":[]}}"));

            Assert.That(result.Granted, Is.EqualTo(3));
            Assert.That(result.Wallet!.Friendship, Is.EqualTo(49));
        }

        [Test]
        public void TheWeeklyBoardIsReadWithTopFriendsAndMine()
        {
            Assert.That(WeeklyBoardView.SubmitPayload(2914, "only_mouse", 120, 2), Is.EqualTo("{\"week\":2914,\"trial\":\"only_mouse\",\"stage\":120,\"heroClass\":2}"));
            Assert.That(WeeklyBoardView.BoardPayload("only_mouse", new[] { "bob" }), Is.EqualTo("{\"trial\":\"only_mouse\",\"friends\":[\"bob\"]}"));
            var board = WeeklyBoardView.From(NakamaJson.Parse("{\"week\":2914,\"trials\":[\"only_mouse\",\"only_keys\",\"no_skills\"],\"trial\":\"only_mouse\","
                + "\"top\":[{\"rank\":1,\"userId\":\"bob\",\"name\":\"Bob\",\"stage\":300,\"heroClass\":5}],"
                + "\"mine\":{\"rank\":2,\"userId\":\"ann\",\"name\":\"Ann\",\"stage\":120,\"heroClass\":2},\"friends\":[]}"));

            Assert.That(board.Week, Is.EqualTo(2914));
            Assert.That(board.Trials, Is.EqualTo(new[] { "only_mouse", "only_keys", "no_skills" }));
            Assert.That(board.Top[0].Stage, Is.EqualTo(300));
            Assert.That(board.Mine!.Rank, Is.EqualTo(2));
            Assert.That(board.Friends, Is.Empty);
            Assert.That(WeeklyBoardView.From(NakamaJson.Parse("{\"week\":1,\"trials\":[],\"mine\":null}")).Mine, Is.Null);
        }

        [Test]
        public void TheClanWarIsReadWithOpponentFrontsAndLastResult()
        {
            Assert.That(ClanWarView.JoinPayload(false), Is.EqualTo("{\"join\":false}"));
            var war = ClanWarView.From(NakamaJson.Parse("{\"week\":2914,\"phase\":\"war\",\"warStartsAt\":1,\"warEndsAt\":2,\"joined\":true,\"active\":5,\"minActive\":3,\"morale\":0.03,"
                + "\"rating\":12,\"league\":\"silver\",\"wins\":4,\"opponent\":{\"clanId\":\"c2\",\"name\":\"lilac lanterns\",\"shadow\":false,\"bye\":false,\"active\":6,\"morale\":0.1},"
                + "\"fronts\":{\"ours\":{\"keys\":2060,\"mouse\":10},\"theirs\":{\"keys\":100,\"mouse\":500},\"mine\":{\"keys\":1000},\"cap\":3600},"
                + "\"last\":{\"week\":2913,\"outcome\":\"win\",\"points\":3,\"theirPoints\":2,\"seals\":150,\"rating\":12,\"league\":\"silver\"}}"));

            Assert.That(war.Phase, Is.EqualTo("war"));
            Assert.That(war.Opponent!.Name, Is.EqualTo("lilac lanterns"));
            Assert.That(war.Fronts, Has.Count.EqualTo(5));
            Assert.That(war.Fronts[0].Leading, Is.True);
            Assert.That(war.Fronts[1].Leading, Is.False);
            Assert.That(war.Fronts[0].Mine, Is.EqualTo(1000));
            Assert.That(war.Cap, Is.EqualTo(3600));
            Assert.That(war.Last!.Outcome, Is.EqualTo("win"));
            Assert.That(war.Last.Seals, Is.EqualTo(150));
            var gather = ClanWarView.From(NakamaJson.Parse("{\"week\":1,\"phase\":\"gather\",\"opponent\":null,\"fronts\":null,\"last\":null}"));
            Assert.That(gather.Opponent, Is.Null);
            Assert.That(gather.Fronts, Is.Empty);
            Assert.That(gather.Last, Is.Null);
        }

        [Test]
        public void CosmeticsAndClanBannersAreReadFromTheServer()
        {
            Assert.That(CosmeticsMineView.BuyPayload("skin_duelist"), Is.EqualTo("{\"id\":\"skin_duelist\"}"));
            Assert.That(ClanCosmeticsView.SetPayload("banner_wolf", null), Is.EqualTo("{\"banner\":\"banner_wolf\"}"));
            var mine = CosmeticsMineView.From(NakamaJson.Parse("{\"owned\":[\"skin_duelist\",\"champion_top100\"],\"crystals\":480,\"fresh\":[\"champion_top100\"]}"));
            Assert.That(mine.Owned, Is.EqualTo(new[] { "skin_duelist", "champion_top100" }));
            Assert.That(mine.Crystals, Is.EqualTo(480));
            Assert.That(mine.Fresh, Is.EqualTo(new[] { "champion_top100" }));
            var clan = ClanCosmeticsView.From(NakamaJson.Parse("{\"owned\":[\"banner_wolf\"],\"banner\":\"banner_wolf\",\"emblem\":\"\",\"victor\":true,\"victorUntil\":5,\"victorBanner\":\"war_victors\"}"));
            Assert.That(clan.ShownBanner, Is.EqualTo("war_victors"));
            Assert.That(clan.Banner, Is.EqualTo("banner_wolf"));
            var wallet = CoopWalletView.From(NakamaJson.Parse("{\"balances\":{\"friendship\":1,\"seals\":2,\"crystals\":300},\"today\":[],\"ledger\":[]}"));
            Assert.That(wallet.Crystals, Is.EqualTo(300));
        }

        [Test]
        public void TheSeasonalEventIsReadFromTheServer()
        {
            Assert.That(EventReply.ProgressPayload("pumpkin_night_2026", 40, 12, new[] { "guests" }, 3000),
                Is.EqualTo("{\"window\":\"pumpkin_night_2026\",\"tokens\":40,\"guests\":12,\"goals\":[\"guests\"],\"score\":3000}"));
            Assert.That(EventReply.BuyPayload("pumpkin_theme"), Is.EqualTo("{\"cosmetic\":\"pumpkin_theme\"}"));
            var reply = EventReply.From(NakamaJson.Parse("{\"window\":\"pumpkin_night_2026\",\"shopWindow\":\"pumpkin_night_2026\",\"endsDay\":20763,\"shopEndsDay\":20766,\"tokens\":340,\"today\":40,\"dailyCap\":300,\"goals\":[\"guests\"],\"owned\":[\"pumpkin_theme\"],\"accepted\":40,\"freshGoals\":[\"guests\"],\"freshCrystals\":0}"));
            Assert.That(reply.Window, Is.EqualTo("pumpkin_night_2026"));
            Assert.That(reply.Tokens, Is.EqualTo(340));
            Assert.That(reply.Accepted, Is.EqualTo(40));
            Assert.That(reply.Goals, Is.EqualTo(new[] { "guests" }));
            Assert.That(reply.Owned, Is.EqualTo(new[] { "pumpkin_theme" }));
            Assert.That(reply.ShopEndsDay, Is.EqualTo(20766));
            var board = EventBoardView.From(NakamaJson.Parse("{\"window\":\"pumpkin_night_2026\",\"top\":[{\"rank\":1,\"userId\":\"a\",\"name\":\"Ann\",\"score\":30000}],\"mine\":null}"));
            Assert.That(board.Top.Single().Score, Is.EqualTo(30000));
            Assert.That(board.Mine, Is.Null);
        }

        [Test]
        public void HideoutVisitsAreReadFromTheServer()
        {
            Assert.That(HideoutGuests.GuestPayload("ann", "water", 2), Is.EqualTo("{\"host\":\"ann\",\"act\":\"water\",\"bed\":2}"));
            var visit = HideoutVisitView.From(NakamaJson.Parse("{\"host\":\"ann\",\"hideout\":{\"theme\":\"ice_peak\",\"decor\":[\"decor_lantern\",\"\"],\"monuments\":{\"milestone\":2},\"title\":\"legends\",\"trophies\":[\"trophy_dragon_koi\"],\"brand\":\"\"},\"left\":[\"water\",\"hammer\"]}"));
            Assert.That(visit.Theme, Is.EqualTo("ice_peak"));
            Assert.That(visit.Monuments["milestone"], Is.EqualTo(2));
            Assert.That(visit.Decor, Is.EqualTo(new[] { "decor_lantern", "" }));
            Assert.That(visit.Left, Is.EqualTo(new[] { "water", "hammer" }));
            var guests = HideoutGuests.Guests(NakamaJson.Parse("{\"guests\":[{\"from\":\"b\",\"fromName\":\"Bob\",\"act\":\"water\",\"bed\":1,\"at\":5}]}"));
            Assert.That(guests.Single().Act, Is.EqualTo("water"));
            Assert.That(guests.Single().Bed, Is.EqualTo(1));
        }

        [Test]
        public void ChatLinesTicketsAndOwnNamesAreReadFromTheServer()
        {
            Assert.That(ChatReply.SendPayload("world", "ru", "hi \"all\"", 7), Is.EqualTo("{\"channel\":\"world\",\"language\":\"ru\",\"text\":\"hi \\\"all\\\"\",\"after\":7}"));
            Assert.That(ChatReply.ReadPayload("party", "en", 0), Is.EqualTo("{\"channel\":\"party\",\"language\":\"en\",\"after\":0}"));
            Assert.That(ChatReply.ReportPayload("clan", "ru", 3, "spam"), Is.EqualTo("{\"channel\":\"clan\",\"language\":\"ru\",\"seq\":3,\"reason\":\"spam\"}"));
            Assert.That(ChatReply.MutePayload("eve", 1.5, "world"), Is.EqualTo("{\"userId\":\"eve\",\"hours\":1.5,\"scope\":\"world\"}"));
            Assert.That(TicketReply.BuyPayload("clan_rename"), Is.EqualTo("{\"id\":\"clan_rename\"}"));
            var reply = ChatReply.From(NakamaJson.Parse("{\"channel\":\"world\",\"room\":\"world-ru\",\"seq\":9,\"role\":\"moderator\",\"canModerate\":true,\"mutedUntil\":0,\"removed\":[4],"
                + "\"messages\":[{\"seq\":8,\"from\":\"d\",\"name\":\"Dev\",\"role\":\"developer\",\"text\":\"patch\",\"at\":1789},{\"seq\":9,\"from\":\"a\",\"name\":\"Ann\",\"role\":\"\",\"text\":\"hi\",\"at\":1790}]}"));
            Assert.That(reply.Lines.Select(line => line.Role), Is.EqualTo(new[] { "developer", string.Empty }));
            Assert.That(reply.Removed, Is.EqualTo(new long[] { 4 }));
            var log = new Game.Core.Party.ChatLog("world", 50);
            reply.ApplyTo(log);
            Assert.That(log.Room, Is.EqualTo("world-ru"));
            Assert.That(log.LastSeq, Is.EqualTo(9));
            Assert.That(log.CanModerate, Is.True);
            var tickets = TicketReply.From(NakamaJson.Parse("{\"tickets\":[{\"id\":\"party_rename\",\"price\":300,\"owned\":2},{\"id\":\"clan_rename\",\"price\":1000,\"owned\":0}]}"));
            Assert.That(tickets.Owned("party_rename"), Is.EqualTo(2));
            Assert.That(tickets.Price("clan_rename"), Is.EqualTo(1000));
            Assert.That(tickets.Owned("unknown"), Is.EqualTo(0));
            var party = PartyView.From(NakamaJson.Parse("{\"id\":\"p\",\"title\":\"night_shift\",\"name\":\"Owls\",\"renameFree\":false,\"members\":[]}"))!;
            Assert.That(party.Name, Is.EqualTo("Owls"));
            Assert.That(party.RenameFree, Is.False);
            var clan = ClanView.From(NakamaJson.Parse("{\"id\":\"c\",\"adjective\":\"lilac\",\"noun\":\"lanterns\",\"name\":\"Order\",\"renameFree\":true,\"members\":[]}"))!;
            Assert.That(clan.Name, Is.EqualTo("Order"));
            Assert.That(clan.RenameFree, Is.True);
        }

        [Test]
        public void TheTreatMarketIsReadFromTheServer()
        {
            Assert.That(MarketReply.ListPayload(string.Empty), Is.EqualTo("{}"));
            Assert.That(MarketReply.ListPayload("treat_bone_broth"), Is.EqualTo("{\"item\":\"treat_bone_broth\"}"));
            Assert.That(MarketReply.SellPayload("d7n1", "treat_bone_broth", 3, 4), Is.EqualTo("{\"nonce\":\"d7n1\",\"item\":\"treat_bone_broth\",\"amount\":3,\"price\":4}"));
            Assert.That(MarketReply.LotPayload("ann_d7n1"), Is.EqualTo("{\"lot\":\"ann_d7n1\"}"));
            var reply = MarketReply.From(NakamaJson.Parse("{\"now\":2000,\"currency\":\"friendship\",\"balance\":80,\"fee\":0.1,\"maxLots\":5,\"paid\":20,"
                + "\"lots\":[{\"id\":\"bob_b1\",\"seller\":\"bob\",\"sellerName\":\"Bob\",\"item\":\"treat_bone_broth\",\"amount\":5,\"price\":4,\"at\":1000,\"until\":9000}],"
                + "\"mine\":[{\"id\":\"ann_a1\",\"seller\":\"ann\",\"sellerName\":\"Ann\",\"item\":\"treat_herb_cookie\",\"amount\":2,\"price\":3,\"at\":500,\"until\":1500}],"
                + "\"inbox\":[{\"id\":\"bob_b0_900\",\"item\":\"treat_bone_broth\",\"amount\":1,\"reason\":\"bought\",\"at\":1900}]}"));
            Assert.That(reply.Balance, Is.EqualTo(80));
            Assert.That(reply.Currency, Is.EqualTo("friendship"));
            Assert.That(reply.Paid, Is.EqualTo(20));
            Assert.That(reply.Lots[0].Total, Is.EqualTo(20));
            Assert.That(reply.Lots[0].SellerName, Is.EqualTo("Bob"));
            Assert.That(reply.Expired(reply.Lots[0]), Is.False);
            Assert.That(reply.Expired(reply.Mine[0]), Is.True);
            Assert.That(reply.Inbox[0].Bought, Is.True);
            Assert.That(reply.Inbox[0].Amount, Is.EqualTo(1));
            Assert.That(MarketReply.From(NakamaJson.Parse("{}")).Lots, Is.Empty);
        }

        [Test]
        public void TheTownGatesAreReadFromTheServer()
        {
            Assert.That(TownReply.GiftPayload("bob", "herb_windwort", 3), Is.EqualTo("{\"to\":\"bob\",\"item\":\"herb_windwort\",\"amount\":3}"));
            Assert.That(TownReply.ClaimPayload(new[] { "ann_1", "bob_2" }), Is.EqualTo("{\"ack\":[\"ann_1\",\"bob_2\"]}"));
            Assert.That(TownReply.ClaimPayload(System.Array.Empty<string>()), Is.EqualTo("{\"ack\":[]}"));
            var reply = TownReply.From(NakamaJson.Parse("{\"guilds\":[\"gardeners\"],\"sentToday\":2,\"giftsPerDay\":5,\"inbox\":[],\"claimed\":[{\"id\":\"g1\",\"from\":\"a\",\"fromName\":\"Ann\",\"item\":\"herb_hushbell\",\"amount\":2,\"at\":1789}]}"));
            Assert.That(reply.Guilds, Is.EqualTo(new[] { "gardeners" }));
            Assert.That(reply.SentToday, Is.EqualTo(2));
            Assert.That(reply.GiftsPerDay, Is.EqualTo(5));
            Assert.That(reply.Inbox, Is.Empty);
            Assert.That(reply.Claimed.Single().FromName, Is.EqualTo("Ann"));
            Assert.That(reply.Claimed.Single().Amount, Is.EqualTo(2));
        }

        [Test]
        public void TheClanFairIsReadWithItsContestsAndPlaces()
        {
            Assert.That(FairReply.FishPayload("fish_hills_pike", 81.25), Is.EqualTo("{\"fish\":\"fish_hills_pike\",\"cm\":81.3}"));
            Assert.That(FairReply.GardenPayload(12, 2), Is.EqualTo("{\"harvests\":12,\"mutations\":2}"));
            var fair = FairReply.From(NakamaJson.Parse("{\"week\":3000,\"endsAt\":1789,\"contests\":[{\"id\":\"big_fish\",\"top\":[{\"rank\":1,\"userId\":\"b\",\"name\":\"Bob\",\"score\":100},{\"rank\":2,\"userId\":\"a\",\"name\":\"Ann\",\"score\":90.5}],"
                + "\"mine\":{\"rank\":2,\"score\":90.5},\"entrants\":2},{\"id\":\"best_garden\",\"top\":[],\"mine\":null,\"entrants\":0}],\"fish\":{\"fish\":\"fish_hills_pike\",\"cm\":81.5,\"score\":90.5}}"));
            Assert.That(fair.Week, Is.EqualTo(3000));
            Assert.That(fair.EndsAtUnixMs, Is.EqualTo(1789));
            var fish = fair.Contest("big_fish")!;
            Assert.That(fish.Top.Select(row => row.Name), Is.EqualTo(new[] { "Bob", "Ann" }));
            Assert.That(fish.MyRank, Is.EqualTo(2));
            Assert.That(fish.MyScore, Is.EqualTo(90.5));
            Assert.That(fair.Contest("best_garden")!.MyRank, Is.Zero);
            Assert.That(fair.BestFish, Is.EqualTo("fish_hills_pike"));
            Assert.That(fair.BestCm, Is.EqualTo(81.5));
        }

        [Test]
        public void CompanionsInTheHideoutAndThePartyTripAreRead()
        {
            Assert.That(CompanionCoopReply.PlacePayload("camp_dog", "Rex", 1), Is.EqualTo("{\"companion\":\"camp_dog\",\"name\":\"Rex\",\"stage\":1}"));
            Assert.That(CompanionCoopReply.PlacePayload("", "", 0), Is.EqualTo("{\"companion\":null}"));
            Assert.That(CompanionCoopReply.OwnerPayload("u1"), Is.EqualTo("{\"owner\":\"u1\"}"));
            Assert.That(CompanionCoopReply.CompanionPayload(""), Is.EqualTo("{\"companion\":null}"));
            var hideout = HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"companions\":{\"pets\":[{\"userId\":\"a\",\"owner\":\"Ann\",\"companion\":\"camp_dog\",\"name\":\"Rex\",\"stage\":2,\"mine\":false}],"
                + "\"petsLeft\":2,\"warmed\":[\"a\"],\"far\":{\"sent\":[{\"owner\":\"Ann\",\"companion\":\"camp_dog\",\"mine\":false},{\"owner\":\"Bob\",\"companion\":\"pet_owl\",\"mine\":true}],"
                + "\"biomes\":[\"green_hills\",\"sky_islands\"],\"endsAt\":1800086400000,\"running\":true,\"ready\":false},\"farLeft\":0}}"));
            var companions = hideout.Companions;
            Assert.That(companions.Pets.Single().Name, Is.EqualTo("Rex"));
            Assert.That(companions.Pets.Single().Stage, Is.EqualTo(2));
            Assert.That(companions.PetsLeft, Is.EqualTo(2));
            Assert.That(companions.Warmed, Is.EqualTo(new[] { "a" }));
            Assert.That(companions.FarSent.Select(member => member.Companion), Is.EqualTo(new[] { "camp_dog", "pet_owl" }));
            Assert.That(companions.FarBiomes.Count, Is.EqualTo(2));
            Assert.That(companions.FarRunning, Is.True);
            Assert.That(companions.FarEndsAt, Is.EqualTo(1800086400000));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":0}")).Companions.Pets, Is.Empty);
        }

        [Test]
        public void TheHideoutIsReadFromTheServerWithWhoIsByTheFire()
        {
            Assert.That(HideoutReply.PresencePayload(true), Is.EqualTo("{\"here\":true}"));
            Assert.That(HideoutReply.PresencePayload(false), Is.EqualTo("{\"here\":false}"));
            var hideout = HideoutReply.From(NakamaJson.Parse("{\"tier\":2,\"members\":3,\"bosses\":14,\"orders\":0,\"sitSeconds\":7300,\"fireLevel\":0,"
                + "\"here\":[{\"userId\":\"a\",\"name\":\"Ann\"},{\"userId\":\"b\",\"name\":\"Bob\"}]}"));
            Assert.That(hideout.Tier, Is.EqualTo(2));
            Assert.That(hideout.Members, Is.EqualTo(3));
            Assert.That(hideout.Bosses, Is.EqualTo(14));
            Assert.That(hideout.SitSeconds, Is.EqualTo(7300));
            Assert.That(hideout.Here, Is.EqualTo(new[] { "Ann", "Bob" }));
            Assert.That(hideout.Fishers, Is.Empty);
            Assert.That(hideout.FishingTogether, Is.False);

            Assert.That(HideoutReply.PresencePayload(false, true, 3), Is.EqualTo("{\"here\":false,\"fishing\":true,\"caught\":3}"));
            var fishing = HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"fishers\":[{\"userId\":\"a\",\"name\":\"Ann\"},{\"userId\":\"b\",\"name\":\"Bob\"}],\"fishingTogether\":true,\"fishTogether\":7}"));
            Assert.That(fishing.Fishers, Is.EqualTo(new[] { "Ann", "Bob" }));
            Assert.That(fishing.FishingTogether, Is.True);
            Assert.That(fishing.FishTogether, Is.EqualTo(7));

            Assert.That(HideoutReply.QueuePayload("bonfire"), Is.EqualTo("{\"building\":\"bonfire\"}"));
            Assert.That(HideoutReply.WantPayload("garden", true), Is.EqualTo("{\"building\":\"garden\",\"on\":true}"));
            var build = HideoutReply.From(NakamaJson.Parse("{\"tier\":2,\"hoursTogether\":12,\"leader\":true,\"buildings\":{\"bonfire\":2},\"canQueue\":[\"board\"],"
                + "\"project\":{\"building\":\"board\",\"level\":1,\"points\":40,\"target\":120,\"hours\":2,\"yours\":15},\"wants\":{\"garden\":2},\"mine\":[\"garden\"],\"here\":[]}"));
            Assert.That(build.Buildings["bonfire"], Is.EqualTo(2));
            Assert.That(build.Project, Is.EqualTo("board"));
            Assert.That(build.ProjectTarget, Is.EqualTo(120));
            Assert.That(build.ProjectYours, Is.EqualTo(15));
            Assert.That(build.Wants["garden"], Is.EqualTo(2));
            Assert.That(build.MyWants, Is.EqualTo(new[] { "garden" }));
            Assert.That(build.Leader, Is.True);
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"project\":null,\"here\":[]}")).Project, Is.Empty);

            Assert.That(HideoutReply.CornerPayload("Huscarl", "", new[] { "decor_lantern", "decor_bench" }),
                Is.EqualTo("{\"hero\":\"Huscarl\",\"path\":\"\",\"decor\":[\"decor_lantern\",\"decor_bench\"]}"));
            var corners = HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"here\":[],\"cornerDecor\":3,\"myCorner\":true,"
                + "\"corners\":[{\"userId\":\"a\",\"name\":\"Ann\",\"slot\":0,\"hero\":\"Huscarl\",\"path\":\"berserker\",\"decor\":[\"decor_bench\"]}]}"));
            Assert.That(corners.MyCorner, Is.True);
            Assert.That(corners.CornerDecor, Is.EqualTo(3));
            Assert.That(corners.Corners[0].Name, Is.EqualTo("Ann"));
            Assert.That(corners.Corners[0].Path, Is.EqualTo("berserker"));
            Assert.That(corners.Corners[0].Decor, Is.EqualTo(new[] { "decor_bench" }));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"here\":[]}")).MyCorner, Is.False);

            Assert.That(HideoutReply.PlantPayload(2, "knockbloom"), Is.EqualTo("{\"bed\":2,\"plant\":\"knockbloom\"}"));
            Assert.That(HideoutReply.TendPayload(true, false, 0, 12.7, 3, 1.5, 60), Is.EqualTo("{\"here\":true,\"fishing\":false,\"caught\":0,\"tend\":{\"keys\":12,\"clicks\":3,\"mouseMeters\":1,\"idleSeconds\":60},\"craft\":null}"));
            Assert.That(HideoutReply.TendPayload(true, false, 0, 0, 0, 0, 0, "forge"), Does.EndWith(",\"craft\":\"forge\"}"));
            Assert.That(HideoutReply.BrewedPayload("might", true), Is.EqualTo("{\"recipe\":\"might\",\"fine\":true}"));
            var crafts = HideoutReply.From(NakamaJson.Parse("{\"tier\":5,\"here\":[],\"crafts\":{\"forge\":[\"Bob\"],\"cauldron\":[]}}"));
            Assert.That(crafts.ForgePartners, Is.EqualTo(new[] { "Bob" }));
            Assert.That(crafts.CauldronPartners, Is.Empty);
            var hall = HideoutReply.From(NakamaJson.Parse("{\"tier\":7,\"here\":[],\"trophies\":{\"bosses\":1200,\"guardians\":4,\"riftDepth\":31,\"hours\":40,\"feats\":[{\"id\":\"old_guard\",\"level\":2}]}}"));
            Assert.That(hall.TrophyGuardians, Is.EqualTo(4));
            Assert.That(hall.TrophyRiftDepth, Is.EqualTo(31));
            Assert.That(hall.TrophyFeats[0].Id, Is.EqualTo("old_guard"));
            Assert.That(hall.TrophyFeats[0].Level, Is.EqualTo(2));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"here\":[]}")).ForgePartners, Is.Empty);
            var commons = HideoutReply.From(NakamaJson.Parse("{\"tier\":3,\"here\":[],\"commons\":{\"together\":true,\"garden\":[{\"plant\":\"knockbloom\",\"share\":0.25,\"ready\":false,\"tended\":true},{\"plant\":\"\",\"share\":0,\"ready\":false,\"tended\":false}],"
                + "\"owed\":{\"herb_knockbloom\":3},\"storage\":{\"ember_coal\":2},\"journal\":[{\"at\":1,\"name\":\"Ann\",\"item\":\"ember_coal\",\"amount\":-2}],\"takenToday\":2,\"takePerDay\":20}}"));
            Assert.That(commons.Together, Is.True);
            Assert.That(commons.Beds[0].Share, Is.EqualTo(0.25));
            Assert.That(commons.Beds[1].Empty, Is.True);
            Assert.That(commons.Owed["herb_knockbloom"], Is.EqualTo(3));
            Assert.That(commons.StorageBuilt, Is.True);
            Assert.That(commons.Storage["ember_coal"], Is.EqualTo(2));
            Assert.That(commons.Journal[0].Amount, Is.EqualTo(-2));
            Assert.That(commons.TakePerDay, Is.EqualTo(20));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":3,\"here\":[],\"commons\":{\"storage\":null}}")).StorageBuilt, Is.False);
            Assert.That(HideoutReply.ClaimedItems(NakamaJson.Parse("{\"items\":{\"herb_knockbloom\":3},\"hideout\":{}}"))["herb_knockbloom"], Is.EqualTo(3));
            Assert.That(HideoutReply.ClaimSeq(NakamaJson.Parse("{\"seq\":1800000000123,\"items\":{}}")), Is.EqualTo(1800000000123));
            Assert.That(HideoutReply.ClaimPayload(1800000000123), Is.EqualTo("{\"ack\":1800000000123}"));
            var ordered = HideoutReply.From(NakamaJson.Parse("{\"tier\":2,\"here\":[],\"order\":{\"id\":\"hunt_and_fish\",\"done\":false,\"daysLeft\":4,\"canRedraw\":true,"
                + "\"needs\":[{\"counter\":\"bosses\",\"have\":7,\"target\":20},{\"counter\":\"fishTogether\",\"have\":0,\"target\":30}]}}"));
            Assert.That(ordered.OrderId, Is.EqualTo("hunt_and_fish"));
            Assert.That(ordered.OrderDaysLeft, Is.EqualTo(4));
            Assert.That(ordered.OrderCanRedraw, Is.True);
            Assert.That(ordered.OrderNeeds[0].Have, Is.EqualTo(7));
            Assert.That(ordered.OrderNeeds[1].Target, Is.EqualTo(30));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":1,\"here\":[],\"order\":null}")).OrderId, Is.Empty);
        }

        [Test]
        public void TheTownBuildIsReadFromTheServer()
        {
            Assert.That(TownBuildReply.AddPayload(new System.Collections.Generic.Dictionary<string, long> { ["labour"] = 1200, ["herbs"] = 7 }), Is.EqualTo("{\"gains\":{\"herbs\":7,\"labour\":1200}}"));
            Assert.That(TownBuildReply.VotePayload("smiths:2"), Is.EqualTo("{\"project\":\"smiths:2\"}"));
            var vote = TownBuildReply.From(NakamaJson.Parse("{\"built\":[\"gardeners\",\"fishers\"],\"round\":2,\"phase\":\"vote\",\"voteEnds\":9000000,"
                + "\"candidates\":[{\"project\":\"smiths\",\"votes\":31},{\"project\":\"gardeners:2\",\"votes\":4}],\"vote\":\"smiths\",\"project\":\"\",\"opensAt\":0,\"now\":1800000,"
                + "\"resources\":[{\"id\":\"labour\",\"stock\":3200,\"need\":0,\"yours\":1500,\"sentToday\":4800,\"dailyCap\":5000}],\"accepted\":{\"labour\":1200},"
                + "\"points\":4100,\"rank\":2,\"top\":[{\"rank\":1,\"name\":\"Mira\",\"points\":9000},{\"rank\":2,\"name\":\"Kay\",\"points\":4100}]}"));
            Assert.That(vote.Board.Points, Is.EqualTo(4100));
            Assert.That(vote.Board.Rank, Is.EqualTo(2));
            Assert.That(vote.Board.Top.Count, Is.EqualTo(2));
            Assert.That(vote.Board.Top[0].Name, Is.EqualTo("Mira"));
            Assert.That(vote.Board.Top[1].Points, Is.EqualTo(4100));
            Assert.That(vote.Built, Is.EqualTo(new[] { "gardeners", "fishers" }));
            Assert.That(vote.Board.Built, Is.EqualTo(new[] { "gardeners", "fishers" }));
            Assert.That(vote.Board.Phase, Is.EqualTo(Game.Core.Sim.TownBoardPhase.Vote));
            Assert.That(vote.Board.Round, Is.EqualTo(2));
            Assert.That(vote.Board.SecondsLeft(0), Is.EqualTo(7200));
            Assert.That(vote.Board.SecondsLeft(200), Is.EqualTo(7000));
            Assert.That(vote.Board.Vote, Is.EqualTo("smiths"));
            Assert.That(vote.Board.Candidates.Count, Is.EqualTo(2));
            Assert.That(vote.Board.Candidates[0].Votes, Is.EqualTo(31));
            Assert.That(vote.Board.Candidates[1].Project, Is.EqualTo("gardeners:2"));
            Assert.That(vote.Board.Stocks[0].Stock, Is.EqualTo(3200));
            Assert.That(vote.Board.Stocks[0].Yours, Is.EqualTo(1500));
            Assert.That(vote.Board.LeftToday("labour"), Is.EqualTo(200));
            Assert.That(vote.Accepted["labour"], Is.EqualTo(1200));

            var opening = TownBuildReply.From(NakamaJson.Parse("{\"built\":[],\"round\":1,\"phase\":\"build\",\"voteEnds\":100,\"candidates\":[],\"vote\":\"\",\"project\":\"smiths\","
                + "\"opensAt\":4600000,\"now\":1000000,\"resources\":[{\"id\":\"labour\",\"stock\":70000,\"need\":63000,\"yours\":0,\"sentToday\":0,\"dailyCap\":5000}],\"accepted\":{}}"));
            Assert.That(opening.Board.Phase, Is.EqualTo(Game.Core.Sim.TownBoardPhase.Opening));
            Assert.That(opening.Board.Project, Is.EqualTo("smiths"));
            Assert.That(opening.Board.SecondsLeft(0), Is.EqualTo(3600));
            Assert.That(opening.Board.Gathered, Is.True);
            Assert.That(opening.Accepted, Is.Empty);
            Assert.That(opening.Board.Points, Is.Zero, "a reply of an older server has no builders");
            Assert.That(opening.Board.Top, Is.Empty);
        }

        [Test]
        public void TheClanCampAndItsFeastAreReadFromTheServer()
        {
            Assert.That(ClanCampReply.GivePayload("herb_windwort", 12), Is.EqualTo("{\"item\":\"herb_windwort\",\"amount\":12}"));
            Assert.That(ClanCampReply.BringPayload("fish_soup"), Is.EqualTo("{\"dish\":\"fish_soup\"}"));
            var camp = ClanCampReply.From(NakamaJson.Parse("{\"points\":1620,\"level\":2,\"next\":4000,\"yours\":300,\"today\":40,\"dailyCap\":300,"
                + "\"feast\":{\"week\":3000,\"dishes\":5,\"target\":5,\"laid\":true,\"yours\":2,\"open\":true,\"until\":1789},\"log\":[]}"));
            Assert.That(camp.Level, Is.EqualTo(2));
            Assert.That(camp.Points, Is.EqualTo(1620));
            Assert.That(camp.Next, Is.EqualTo(4000));
            Assert.That(camp.Today, Is.EqualTo(40));
            Assert.That(camp.DailyCap, Is.EqualTo(300));
            Assert.That(camp.FeastLaid, Is.True);
            Assert.That(camp.FeastOpen, Is.True);
            Assert.That(camp.FeastDishes, Is.EqualTo(5));
            Assert.That(camp.FeastYours, Is.EqualTo(2));
            Assert.That(camp.FeastUntilUnixMs, Is.EqualTo(1789));
        }

        [Test]
        public void ATournamentIsReadFromTheServerWithTheGroupAndMyRow()
        {
            Assert.That(TournamentReply.JoinPayload(12000), Is.EqualTo("{\"record\":12000}"));
            Assert.That(TournamentReply.SubmitPayload(6001, 340), Is.EqualTo("{\"index\":6001,\"score\":340}"));
            var reply = TournamentReply.From(NakamaJson.Parse("{\"index\":6001,\"format\":\"bosses\",\"startsAt\":100,\"endsAt\":900,\"joinUntil\":800,\"joined\":true,\"band\":2,\"group\":0,"
                + "\"top\":[{\"rank\":1,\"userId\":\"bob\",\"name\":\"Bob\",\"score\":500},{\"rank\":2,\"userId\":\"ann\",\"name\":\"Ann\",\"score\":340}],"
                + "\"mine\":{\"rank\":2,\"userId\":\"ann\",\"name\":\"Ann\",\"score\":340},\"fresh\":[\"tournament-6000\"]}"));
            Assert.That(reply.Index, Is.EqualTo(6001));
            Assert.That(reply.Format, Is.EqualTo("bosses"));
            Assert.That(reply.Joined, Is.True);
            Assert.That(reply.Band, Is.EqualTo(2));
            Assert.That(reply.Top.Count, Is.EqualTo(2));
            Assert.That(reply.Top[0].Name, Is.EqualTo("Bob"));
            Assert.That(reply.Mine!.Value.Rank, Is.EqualTo(2));
            Assert.That(reply.Mine!.Value.Score, Is.EqualTo(340));
            Assert.That(reply.Fresh, Is.EqualTo(new[] { "tournament-6000" }));
            Assert.That(reply.JoinOpen(799), Is.True);
            Assert.That(reply.JoinOpen(800), Is.False);
            var outside = TournamentReply.From(NakamaJson.Parse("{\"index\":6002,\"format\":\"kills\",\"joined\":false,\"band\":-1,\"group\":-1,\"top\":[],\"mine\":null,\"fresh\":[]}"));
            Assert.That(outside.Joined, Is.False);
            Assert.That(outside.Mine, Is.Null);
        }

        [Test]
        public void TheChestShopAndAChestLetterAreReadFromTheServer()
        {
            Assert.That(ChestShopReply.BuyPayload("smith"), Is.EqualTo("{\"id\":\"smith\"}"));
            var shop = ChestShopReply.From(NakamaJson.Parse("{\"week\":3000,\"bought\":{\"smith\":1,\"master\":1},\"crystals\":300,\"letter\":\"chest-3000-smith-1\"}"));
            Assert.That(shop.Week, Is.EqualTo(3000));
            Assert.That(shop.Crystals, Is.EqualTo(300));
            Assert.That(shop.Letter, Is.EqualTo("chest-3000-smith-1"));
            Assert.That(shop.Bought["smith"], Is.EqualTo(1));
            Assert.That(shop.Bought.ContainsKey("jeweler"), Is.False);
            Assert.That(ChestShopReply.From(NakamaJson.Parse("{\"week\":3000,\"bought\":{},\"crystals\":0,\"letter\":\"\"}")).Bought, Is.Empty);

            var letter = MailDirectory.Letter(NakamaJson.Parse("{\"id\":\"chest-3000-master-1\",\"kind\":\"chest\",\"at\":1,\"place\":0,\"of\":0,\"depth\":0,\"title\":\"master\","
                + "\"reward\":{\"shards\":0,\"ingots\":200,\"essence\":200},\"claimed\":false,\"claimedAt\":0}"));
            Assert.That(letter.Kind, Is.EqualTo(MailLetter.ChestKind));
            Assert.That(letter.Reward.Ingots, Is.EqualTo(200));
            Assert.That(letter.Reward.Essence, Is.EqualTo(200));
        }

        [Test]
        public void TheSeasonPassAndCrystalChangesAreReadFromTheServer()
        {
            Assert.That(SeasonPassReply.AddPayload(450), Is.EqualTo("{\"points\":450}"));
            Assert.That(CrystalChange.Payload(CrystalChangeReason.Class), Is.EqualTo("{\"reason\":\"class_change\"}"));
            Assert.That(CrystalChange.Payload(CrystalChangeReason.Subclass), Is.EqualTo("{\"reason\":\"subclass_change\"}"));
            var pass = SeasonPassReply.From(NakamaJson.Parse("{\"season\":0,\"id\":\"embers\",\"points\":4050,\"level\":10,\"premium\":true,\"today\":450,\"dailyCap\":450,\"endsAt\":1789,\"accepted\":300,\"fresh\":[\"pass1_avatar_gold\"],\"freshCrystals\":50,\"crystals\":60}"));
            Assert.That(pass.Season, Is.EqualTo(0));
            Assert.That(pass.Id, Is.EqualTo("embers"));
            Assert.That(pass.Points, Is.EqualTo(4050));
            Assert.That(pass.Level, Is.EqualTo(10));
            Assert.That(pass.Premium, Is.True);
            Assert.That(pass.Today, Is.EqualTo(450));
            Assert.That(pass.DailyCap, Is.EqualTo(450));
            Assert.That(pass.EndsAtUnixMs, Is.EqualTo(1789));
            Assert.That(pass.Accepted, Is.EqualTo(300));
            Assert.That(pass.Fresh, Is.EqualTo(new[] { "pass1_avatar_gold" }));
            Assert.That(pass.FreshCrystals, Is.EqualTo(50));
            Assert.That(pass.Crystals, Is.EqualTo(60));
            Assert.That(SeasonPassReply.From(NakamaJson.Parse("{\"season\":-1,\"fresh\":[]}")).Season, Is.EqualTo(-1));
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
        public void AGhostIsReadFromTheServerReply()
        {
            var json = NakamaJson.Parse("{\"userId\":\"u1\",\"name\":\"Ann\",\"heroClass\":8,\"shareRate\":0.4,\"activity\":0.75}");

            var ghost = GhostDirectory.Snapshot(json);

            Assert.That(ghost.Name, Is.EqualTo("Ann"));
            Assert.That(ghost.HeroClass, Is.EqualTo(Game.Core.Sim.HeroClass.Whisperer));
            Assert.That(ghost.ShareRate, Is.EqualTo(0.4));
            Assert.That(ghost.Activity, Is.EqualTo(0.75));
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
        public void TheStorageForTheBuildAndTheNameConsentAreRead()
        {
            Assert.That(HideoutReply.SharePayload(new System.Collections.Generic.Dictionary<string, long> { ["ore_hills"] = 15 }), Is.EqualTo("{\"items\":{\"ore_hills\":15}}"));
            var hideout = HideoutReply.From(NakamaJson.Parse("{\"tier\":3,\"project\":{\"building\":\"storage\",\"level\":2,\"points\":40,\"target\":1000,\"hours\":30,\"yours\":0,\"fromStorage\":12},"
                + "\"commons\":{\"storage\":{\"ore_hills\":4},\"forBuild\":{\"points\":8,\"items\":{\"ore_hills\":4}},\"journal\":[{\"name\":\"Bob\",\"item\":\"ore_hills\",\"amount\":-6,\"build\":\"storage\"}]}}"));
            Assert.That(hideout.ProjectFromStorage, Is.EqualTo(12));
            Assert.That(hideout.ForBuildPoints, Is.EqualTo(8));
            Assert.That(hideout.Journal.Single().Build, Is.EqualTo("storage"));
            Assert.That(NameConsentReply.SetPayload(NameConsentReply.TownTop, true), Is.EqualTo("{\"place\":\"town_top\",\"shown\":true}"));
            Assert.That(NameConsentReply.From(NakamaJson.Parse("{\"shown\":[\"town_top\"]}")).Shows(NameConsentReply.TownTop), Is.True);
            Assert.That(NameConsentReply.From(NakamaJson.Parse("{\"shown\":[]}")).Shows(NameConsentReply.Museum), Is.False);
            Assert.That(TownReply.From(NakamaJson.Parse("{\"gatherLeft\":{\"herb_\":150}}")).GatherLeft["herb_"], Is.EqualTo(150));
            var stations = HideoutReply.From(NakamaJson.Parse("{\"tier\":2,\"stations\":{\"garden_bed\":{\"level\":3,\"branch\":\"greenhouse\"}}}"));
            Assert.That(stations.StationLevels["garden_bed"], Is.EqualTo(3));
            Assert.That(stations.StationBranches["garden_bed"], Is.EqualTo("greenhouse"));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":0}")).StationLevels, Is.Empty);
            Assert.That(HideoutReply.BranchPayload("forge", "armory"), Is.EqualTo("{\"station\":\"forge\",\"branch\":\"armory\"}"));
            Assert.That(HideoutReply.From(NakamaJson.Parse("{\"tier\":2,\"modules\":{\"iron_tripod\":0,\"signal_horn\":-1}}")).Modules["signal_horn"], Is.EqualTo(-1));
            Assert.That(HideoutReply.InstallPayload("iron_tripod", 0), Is.EqualTo("{\"module\":\"iron_tripod\",\"socket\":0}"));
        }

        [Test]
        public void TheTownRolesAreReadAndShown()
        {
            var reply = TownBuildReply.From(NakamaJson.Parse("{\"phase\":\"gather\",\"round\":2,\"roles\":[{\"role\":\"feeder\",\"name\":\"Cid\",\"amount\":4},{\"role\":\"logger\",\"name\":\"\",\"amount\":10}]}"));
            Assert.That(reply.Board.Roles.Count, Is.EqualTo(2));
            Assert.That(reply.Board.Roles[0].Name, Is.EqualTo("Cid"));
        }

        [Test]
        public void ARiftSupplyIsReadAndSent()
        {
            Assert.That(RiftSupplyReply.SendPayload("bob", "dish:carp_pie"), Is.EqualTo("{\"to\":\"bob\",\"item\":\"dish:carp_pie\"}"));
            var mine = RiftSupplyReply.From(NakamaJson.Parse("{\"window\":1761652800,\"supply\":{\"window\":1761652800,\"from\":\"a\",\"fromName\":\"Ann\",\"item\":\"potion:might:fine\",\"at\":5}}"));
            Assert.That(mine.Window, Is.EqualTo(1761652800));
            Assert.That(mine.Item, Is.EqualTo("potion:might:fine"));
            Assert.That(mine.FromName, Is.EqualTo("Ann"));
            Assert.That(RiftSupplyReply.From(NakamaJson.Parse("{\"window\":0,\"supply\":null}")).HasSupply, Is.False);
            var rear = RiftSupplyReply.From(NakamaJson.Parse("{\"window\":5,\"endsAt\":9,\"sentThisWindow\":1,\"sendsPerWindow\":3,\"mine\":null,\"fighters\":[{\"userId\":\"c\",\"name\":\"Cid\",\"party\":true,\"supplied\":false}]}"));
            Assert.That(rear.Fighters.Single().Name, Is.EqualTo("Cid"));
            Assert.That(rear.Fighters.Single().Party, Is.True);
            Assert.That(rear.SendsPerWindow, Is.EqualTo(3));
            Assert.That(rear.HasSupply, Is.False);
        }

        [Test]
        public void ObjectsAreWrittenWithEscapedStrings() =>
            Assert.That(NakamaJson.Object(("code", NakamaJson.Quote("a\"b")), ("n", "4")), Is.EqualTo("{\"code\":\"a\\\"b\",\"n\":4}"));
    }
}
