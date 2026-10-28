using System.Linq;
using Game.Core.Party;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PartySearchTests
    {
        private static PartySearchRules Search => TestWorld.Real.Party.Search;

        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [Test]
        public void FiltersStartWithYourLanguageAndNoOtherLimits()
        {
            var filters = new PartySearchFilters(Search, "en");

            Assert.That(filters.Language, Is.EqualTo("en"));
            Assert.That(filters.Slot, Is.Null);
            Assert.That(filters.Wanted, Is.Null);
            Assert.That(filters.LanguageLabel, Is.EqualTo("Язык: English"));
            Assert.That(filters.SlotLabel, Is.EqualTo("Когда: в любое время"));
            Assert.That(filters.WantedLabel, Is.EqualTo("Ищут: все отряды"));
        }

        [Test]
        public void AnUnknownLanguageFallsBackToTheFirstOne() => Assert.That(new PartySearchFilters(Search, "de").Language, Is.EqualTo(Search.Languages[0]));

        [Test]
        public void EachFilterCyclesThroughItsValuesAndBackToAny()
        {
            var filters = new PartySearchFilters(Search, "ru");

            var slots = Enumerable.Range(0, Search.TimeSlots.Count + 1).Select(_ => { filters.NextSlot(); return filters.Slot; }).ToArray();
            var wanted = Enumerable.Range(0, Search.Roles.Count + 1).Select(_ => { filters.NextWanted(); return filters.Wanted; }).ToArray();
            filters.NextLanguage();

            Assert.That(slots, Is.EqualTo(Search.TimeSlots.Select(slot => slot.Id).Append(null)));
            Assert.That(wanted, Is.EqualTo(Search.Roles.Append(null)));
            Assert.That(filters.Language, Is.EqualTo("en"));
            Assert.That(filters.Version, Is.EqualTo(Search.TimeSlots.Count + Search.Roles.Count + 3));
        }

        [Test]
        public void TheCardOfAnOpenPartyReadsInOneLine()
        {
            var row = PartySearchRow.From("p1", "night_shift", "ru", "debuffer", 2, 4, true, "evening", true);

            Assert.That(row.PartyId, Is.EqualTo("p1"));
            Assert.That(row.Title, Is.EqualTo("Ночная смена"));
            Assert.That(row.Details, Is.EqualTo("Русский · Обычно в сети вечером · Ищут: дебаффер"));
            Assert.That(row.Seats, Is.EqualTo("2/4"));
            Assert.That(row.Tags, Is.EqualTo("в бою сейчас · нужна ваша роль"));
        }

        [Test]
        public void MissingOrUnknownFieldsAreLeftOut()
        {
            var row = PartySearchRow.From("p2", "", "de", "", 1, 4, false, null, false);

            Assert.That(row.Title, Is.EqualTo("Отряд без названия"));
            Assert.That(row.Details, Is.EqualTo("DE"));
            Assert.That(row.Tags, Is.Empty);
        }

        [Test]
        public void TheMemberMenuBlocksOrReportsForEveryKnownReason()
        {
            var options = PartyMemberMenu.Options(Search);

            Assert.That(options[0].IsBlock, Is.True);
            Assert.That(options[0].Label, Is.EqualTo("Заблокировать"));
            Assert.That(options.Skip(1).Select(option => option.ReportReason), Is.EqualTo(Search.ReportReasons));
            Assert.That(options[1].Label, Is.EqualTo("Жалоба: Нечестный урон"));
        }

        [Test]
        public void EveryNewTextExistsInBothLanguages()
        {
            foreach (var language in new[] { "ru", "en" })
            {
                Texts.UseLanguage(language);
                foreach (var text in new[] { PartySearchTexts.Quick, PartySearchTexts.QuickHint, PartySearchTexts.Refresh, PartySearchTexts.Join, PartySearchTexts.Loading,
                             PartySearchTexts.Empty, PartySearchTexts.InParty, PartySearchTexts.Offline, PartySearchTexts.Found(3), PartyMemberMenu.Title("A"),
                             PartyMemberMenu.Blocked, PartyMemberMenu.Reported, PartyMemberMenu.NotFound, PartyMemberMenu.Close, PartyActions.Failed("party not found") })
                    Assert.That(text, Does.Not.StartWith("party."), language);
            }
        }
    }
}
