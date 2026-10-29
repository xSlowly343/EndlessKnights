using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class ClanRulesTests
    {
        private const string Valid = @"{
  ""version"": 1,
  ""maxMembers"": 30,
  ""maxOfficers"": 4,
  ""codeLength"": 6,
  ""listLimit"": 20,
  ""activeMinutes"": 15,
  ""inactiveDays"": 30,
  ""leaderAwayDays"": 14,
  ""requests"": { ""days"": 7, ""perClan"": 30, ""perPlayer"": 3 },
  ""names"": { ""adjectives"": [""lilac"", ""golden""], ""nouns"": [""lanterns""] }
}";

        private static ClanRules Real => ClanRules.Load(TestData.ReadGameDataFile(ClanRules.FileName));

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [Test]
        public void TheGameFileHoldsAClanOfThirty()
        {
            var rules = Real;

            Assert.That(rules.MaxMembers, Is.EqualTo(30));
            Assert.That(rules.MaxOfficers, Is.LessThan(rules.MaxMembers));
            Assert.That(rules.LeaderAwayDays, Is.LessThanOrEqualTo(rules.InactiveDays));
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void EveryNameWordAndRoleHasATextInBothLanguages(string language)
        {
            Texts.UseLanguage(language);
            var rules = Real;
            foreach (var id in rules.Adjectives)
                Assert.That(Texts.Has("clan.adjective." + id), Is.True, id);
            foreach (var id in rules.Nouns)
                Assert.That(Texts.Has("clan.noun." + id), Is.True, id);
            foreach (var role in new[] { ClanRules.Leader, ClanRules.Officer, ClanRules.Member })
                Assert.That(Texts.Has("clan.role." + role), Is.True, role);
        }

        [Test]
        public void ClanRequestsReadAsPlainWords()
        {
            var rules = Real;
            Assert.That(rules.RequestDays, Is.EqualTo(7));
            Assert.That(rules.RequestsPerPlayer, Is.EqualTo(3));
            Assert.That(ClanText.RequestSent("lilac", "lanterns"), Is.EqualTo("Заявка в «Лиловые фонари» отправлена"));
            Assert.That(ClanText.RequestFrom("Боря", 0, 2 * 86_400_000L + 1, rules.RequestDays), Is.EqualTo("Боря просится в клан · ещё 5 д"));
            Assert.That(ClanText.Answer(true, "lilac", "lanterns"), Is.EqualTo("Вас приняли в «Лиловые фонари»"));
            Assert.That(ClanText.Failed("too many requests"), Does.Contain("слишком много заявок"));
            Assert.That(ClanText.ByRequest, Is.EqualTo("по заявке"));
        }

        [Test]
        public void TheClanChronicleFileLoadsAndEveryFeatReads()
        {
            var rules = ClanChronicleRules.Load(TestData.ReadGameDataFile(ClanChronicleRules.FileName));
            Assert.That(rules.Feats.Count, Is.EqualTo(6));
            foreach (var language in new[] { "ru", "en" })
            {
                Texts.UseLanguage(language);
                foreach (var feat in rules.Feats)
                {
                    Assert.That(Texts.Has("chronicle.feat." + feat.Id), Is.True, feat.Id);
                    Assert.That(Texts.Has("chronicle.goal." + feat.Kind), Is.True, feat.Kind);
                    if (feat.Title != null)
                        Assert.That(Texts.Has("chronicle.title." + feat.Title), Is.True, feat.Title);
                }
            }
            Texts.UseLanguage("ru");
            Assert.That(PartyChronicleText.Goal("raidsKilled", true, 1, new double[] { 5, 20, 50 }, 3), Is.EqualTo("рейдов, где босс пал: 3 из 20 · ступень 2 из 3"));
            Assert.That(PartyChronicleText.Record("founded", 0, "Аня", "", 180), Is.EqualTo("01.01 — клан основан, глава Аня"));
            Assert.Throws<ContentException>(() => ClanChronicleRules.Load(TestData.ReadGameDataFile(ClanChronicleRules.FileName).Replace("\"treeNodes\"", "\"bossesTogether\"")));
        }

        [Test]
        public void EveryPairOfWordsGivesADifferentName()
        {
            var rules = Real;
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var adjective in rules.Adjectives)
            {
                foreach (var noun in rules.Nouns)
                    Assert.That(names.Add(ClanText.Name(adjective, noun)), Is.True, adjective + " " + noun);
            }
        }

        [Test]
        public void TheNameReadsAsTwoWordsAndTheTagTakesTheirFirstLetters()
        {
            Assert.That(ClanText.Name("lilac", "lanterns"), Is.EqualTo("Лиловые фонари"));
            Assert.That(ClanText.Tag("lilac", "lanterns"), Is.EqualTo("ЛФ"));
            Texts.UseLanguage("en");
            Assert.That(ClanText.Name("lilac", "lanterns"), Is.EqualTo("Lilac Lanterns"));
            Assert.That(ClanText.Tag("lilac", "lanterns"), Is.EqualTo("LL"));
        }

        [Test]
        public void ARowSaysWhenTheMemberWasLastAround()
        {
            const long now = 1_800_000_000_000;
            const long hour = 3_600_000;

            Assert.That(ClanText.Seen(now - 5 * 60_000, now, 15), Is.EqualTo("в игре"));
            Assert.That(ClanText.Seen(now - 3 * hour, now, 15), Is.EqualTo("3 ч назад"));
            Assert.That(ClanText.Seen(now - 50 * hour, now, 15), Is.EqualTo("2 д назад"));
            Assert.That(ClanText.Seen(0, now, 15), Is.EqualTo("давно"));
            Assert.That(ClanText.Members(27, 30), Is.EqualTo("27 из 30"));
            Assert.That(ClanText.Code("K7MQ2X"), Is.EqualTo("Код клана: K7M-Q2X"));
        }

        [Test]
        public void ServerRefusalsReadAsPlainWords()
        {
            Assert.That(ClanText.Failed("clan is full"), Is.EqualTo("Не вышло: в клане нет мест"));
            Assert.That(ClanText.Failed("too many officers"), Does.Contain("офицеров"));
            Assert.That(ClanText.Failed("socket closed"), Is.EqualTo("Не вышло: нет связи с сервером"));
        }

        [TestCase(@"""maxOfficers"": 4", @"""maxOfficers"": 30", "maxOfficers")]
        [TestCase(@"""leaderAwayDays"": 14", @"""leaderAwayDays"": 40", "inactiveDays")]
        [TestCase(@"[""lilac"", ""golden""]", @"[""lilac"", ""lilac""]", "repeated")]
        [TestCase(@"[""lanterns""]", @"[""Lanterns""]", "a-z")]
        [TestCase(@"[""lanterns""]", @"[]", "must not be empty")]
        [TestCase(@"""version"": 1", @"""version"": 2", "not supported")]
        public void BadRulesAreRejected(string from, string to, string message)
        {
            var error = Assert.Throws<ContentException>(() => ClanRules.Load(Valid.Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }
    }
}
