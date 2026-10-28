using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartySearchRulesTests
    {
        private static PartySearchRules Search => TestWorld.Real.Party.Search;

        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [Test]
        public void EveryClassHasARoleThatIsNotAny()
        {
            foreach (var pair in HeroClassNames.ByName)
            {
                var role = Search.RoleOf(pair.Value);
                Assert.That(role, Is.Not.EqualTo(PartySearchRules.AnyRole), pair.Key);
                Assert.That(Search.Roles, Does.Contain(role), pair.Key);
            }
            Assert.That(Search.RoleOf(HeroClass.Voodoo), Is.EqualTo("debuffer"));
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void EveryIdHasATextInBothLanguages(string language)
        {
            Texts.UseLanguage(language);
            foreach (var id in Search.Titles)
                Assert.That(Texts.Has("party.title." + id), Is.True, id);
            foreach (var id in Search.Roles)
                Assert.That(Texts.Has("party.role." + id), Is.True, id);
            foreach (var id in Search.ReportReasons)
                Assert.That(Texts.Has("party.report." + id), Is.True, id);
            foreach (var slot in Search.TimeSlots)
                Assert.That(Texts.Has("party.time." + slot.Id), Is.True, slot.Id);
            foreach (var id in Search.Languages)
                Assert.That(Texts.Has("settings.language." + id), Is.True, id);
        }

        [TestCase(0, "night")]
        [TestCase(5, "night")]
        [TestCase(6, "morning")]
        [TestCase(13, "day")]
        [TestCase(23, "evening")]
        public void LocalHoursFallIntoTimeSlots(int hour, string slot) => Assert.That(Search.TimeSlotAt(hour), Is.EqualTo(slot));

        [Test]
        public void CardTextsAreFormatted()
        {
            Assert.That(PartyCardText.Code("K7MQ2X"), Is.EqualTo("Код отряда: K7M-Q2X"));
            Assert.That(PartyCardText.Wanted("buffer"), Is.EqualTo("Ищут: баффер"));
            Assert.That(PartyCardText.UsuallyOnline("evening"), Is.EqualTo("Обычно в сети вечером"));
            Assert.That(PartyCardText.Title("night_shift"), Is.EqualTo("Ночная смена"));
            Assert.That(PartyCardText.Language("en"), Is.EqualTo("English"));
        }

        [Test]
        public void TheCodeAlphabetHasNoLookAlikes()
        {
            foreach (var c in "01IO")
                Assert.That(PartySearchRules.CodeAlphabet, Does.Not.Contain(c.ToString()));
            Assert.That(PartySearchRules.CodeAlphabet.Length, Is.EqualTo(32));
        }

        [Test]
        public void AClassWithoutARoleIsRejected()
        {
            var json = TestData.ReadGameDataFile(PartyRules.FileName).Replace(@"""captain"": ""summoner""", @"""captain"": ""any""");

            var error = Assert.Throws<ContentException>(() => PartyRules.Load(json));

            Assert.That(error!.Message, Does.Contain("captain"));
        }
    }
}
