using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class CoopCurrencyRulesTests
    {
        private const string Valid = @"{
  ""version"": 1,
  ""currencies"": [""friendship"", ""seals""],
  ""dayUtcOffsetMinutes"": 180,
  ""ledgerSize"": 20,
  ""sources"": [
    { ""id"": ""boss_together"", ""currency"": ""friendship"", ""amount"": 1, ""dailyCap"": 20 },
    { ""id"": ""ghost_helped"", ""currency"": ""friendship"", ""amount"": 1, ""dailyCap"": 10, ""perFriendDailyCap"": 3 }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [TestCase("ru")]
        [TestCase("en")]
        public void TheGameFileLoadsAndEveryCurrencyAndSourceHasATextInBothLanguages(string language)
        {
            Texts.UseLanguage(language);
            var rules = CoopCurrencyRules.Load(TestData.ReadGameDataFile(CoopCurrencyRules.FileName));

            Assert.That(rules.Sources, Is.Not.Empty);
            foreach (var currency in rules.Currencies)
                Assert.That(Texts.Has("wallet.currency." + currency), Is.True, currency);
            foreach (var source in rules.Sources)
                Assert.That(Texts.Has("wallet.source." + source.Id), Is.True, source.Id);
        }

        [Test]
        public void SourcesAreRead()
        {
            var rules = CoopCurrencyRules.Load(Valid);

            Assert.That(rules.DayUtcOffsetMinutes, Is.EqualTo(180), "the day turns by Moscow time");
            Assert.That(rules.Sources[0].PerFriendDailyCap, Is.Zero);
            Assert.That(rules.Sources[1].PerFriendDailyCap, Is.EqualTo(3));
            Assert.That(rules.Sources[1].DailyCap, Is.EqualTo(10));
        }

        [TestCase(@"[""friendship"", ""seals""]", @"[""friendship"", ""gold""]", "unknown currency")]
        [TestCase(@"[""friendship"", ""seals""]", @"[""friendship""]", "must list")]
        [TestCase(@"""currency"": ""friendship"", ""amount"": 1, ""dailyCap"": 20", @"""currency"": ""gems"", ""amount"": 1, ""dailyCap"": 20", "not one of currencies")]
        [TestCase(@"""dailyCap"": 10, ""perFriendDailyCap"": 3", @"""dailyCap"": 10, ""perFriendDailyCap"": 30", "perFriendDailyCap")]
        [TestCase(@"""id"": ""ghost_helped""", @"""id"": ""boss_together""", "new, non-empty id")]
        public void BadRulesAreRejected(string from, string to, string message)
        {
            var error = Assert.Throws<ContentException>(() => CoopCurrencyRules.Load(Valid.Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }

        [Test]
        public void WalletLinesReadPlainly()
        {
            Assert.That(CoopWalletText.Balance("seals", 320), Is.EqualTo("Печати клана 320"));
            Assert.That(CoopWalletText.Entry("friendship", 4, "boss_together"), Is.EqualTo("Знаки дружбы +4 · боссы вместе с отрядом"));
            Assert.That(CoopWalletText.Today("ghost_helped", 3, 10), Is.EqualTo("ваш призрак помог другу: 3 из 10 сегодня"));
        }
    }
}
