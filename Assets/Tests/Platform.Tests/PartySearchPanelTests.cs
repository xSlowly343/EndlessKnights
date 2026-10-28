using System.IO;
using Game.Core.Content;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class PartySearchPanelTests
    {
        private PanelCanvas _panel = null!;
        private PartySearchFilters _filters = null!;

        [SetUp]
        public void SetUp()
        {
            var data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            Texts.UseLanguage("ru");
            _panel = PanelCanvas.Create();
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Party);
            navigator.SelectSubTab(PanelCanvas.PartySearchSubTab);
            _panel.Show(navigator);
            _panel.Place(0f, true);
            _filters = new PartySearchFilters(data.Party.Search, "ru");
            _panel.PartySearch.UseFilters(_filters);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void TheSearchTabReplacesThePlaceholder()
        {
            Assert.That(_panel.PartySearch.IsShown, Is.True);
            Assert.That(_panel.Party.IsShown, Is.False);
        }

        [Test]
        public void OpenPartiesAreListedAndJoinedById()
        {
            string? joined = null;
            _panel.PartySearch.JoinRequested += id => joined = id;
            _panel.PartySearch.ShowRows(new[]
            {
                PartySearchRow.From("p1", "night_shift", "ru", "debuffer", 2, 4, true, "evening", true),
                PartySearchRow.From("p2", "lunch_raid", "ru", "any", 1, 4, false, null, false),
            });
            Canvas.ForceUpdateCanvases();

            Assert.That(_panel.PartySearch.ShownRows, Is.EqualTo(2));
            Assert.That(_panel.TryClick(_panel.PartySearch.WorldPointOfJoin(1)), Is.True);
            Assert.That(joined, Is.EqualTo("p2"));
        }

        [Test]
        public void AFilterClickChangesTheFilterAndAsksForANewList()
        {
            var refreshed = 0;
            _panel.PartySearch.RefreshRequested += () => refreshed++;

            Assert.That(_panel.TryClick(_panel.PartySearch.WorldPointOfFilter(1)), Is.True);

            Assert.That(_filters.Slot, Is.Not.Null);
            Assert.That(refreshed, Is.EqualTo(1));
        }

        [Test]
        public void AnEmptyListSaysHowToStartAParty()
        {
            var quick = 0;
            _panel.PartySearch.QuickRequested += () => quick++;
            _panel.PartySearch.ShowRows(System.Array.Empty<PartySearchRow>());

            Assert.That(_panel.PartySearch.StatusText, Is.EqualTo(PartySearchTexts.Empty));
            Assert.That(_panel.TryClick(_panel.PartySearch.WorldPointOfQuick()), Is.True);
            Assert.That(quick, Is.EqualTo(1));
        }
    }
}
