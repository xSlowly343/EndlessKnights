using System.IO;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class BookPanelTests
    {
        private PanelCanvas _panel = null!;
        private GameLoop _loop = null!;
        private PanelNavigator _navigator = null!;

        [SetUp]
        public void SetUp()
        {
            var data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            _loop = new GameLoop(data, new FakeInputSource(), new GameState { Class = HeroClass.Viking, RecordStage = 500 }, new StatSheet());
            _panel = PanelCanvas.Create();
            _panel.Book.Bind(_loop);
            _navigator = new PanelNavigator();
            _navigator.SelectTab(PanelSection.Book);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void DraggingABagSkillOntoAnEmptyRowEquipsIt()
        {
            var skill = _loop.Data.Gems.All.First(gem => gem.Kind == GemKind.Skill && gem.Skill!.Weapon == null && _loop.Book.MeetsRequirements(gem));
            _loop.State.OwnedGems.Add(skill);
            Open();

            var from = _panel.Book.WorldPointOfBagCell(0);
            var to = _panel.Book.WorldPointOfRow(0);
            Assert.That(_panel.TryClick(from), Is.True);
            _panel.Drag(Vector2.Lerp(from, to, 0.5f));
            _panel.Drag(to);
            _panel.Release(to);

            Assert.That(_loop.State.Skills.Select(slot => slot.Skill), Does.Contain(skill));
            Assert.That(_panel.Book.IsDragging, Is.False);
        }

        [Test]
        public void ClickWithoutDragDescribesTheGem()
        {
            var skill = _loop.Data.Gems.All.First(gem => gem.Kind == GemKind.Skill);
            _loop.State.OwnedGems.Add(skill);
            Open();

            var cell = _panel.Book.WorldPointOfBagCell(0);
            _panel.TryClick(cell);
            _panel.Release(cell);

            Assert.That(_panel.Book.Hint, Is.EqualTo(BookTexts.Describe(skill, 1)));
        }

        [Test]
        public void ClickingAnEquippedSkillOffersToRemoveItToTheBag()
        {
            var skill = _loop.Data.Gems.All.First(gem => gem.Kind == GemKind.Skill && gem.Skill!.Weapon == null && _loop.Book.MeetsRequirements(gem));
            _loop.State.OwnedGems.Add(skill);
            Open();
            var bag = _panel.Book.WorldPointOfBagCell(0);
            var row = _panel.Book.WorldPointOfRow(0);
            _panel.TryClick(bag);
            _panel.Drag(Vector2.Lerp(bag, row, 0.5f));
            _panel.Drag(row);
            _panel.Release(row);
            var index = _loop.State.Skills.FindIndex(slot => ReferenceEquals(slot.Skill, skill));
            Assert.That(index, Is.GreaterThanOrEqualTo(0));

            var card = _panel.Book.WorldPointOfRow(index);
            _panel.TryClick(card);
            _panel.Release(card);
            Assert.That(_panel.Book.SelectedRow, Is.EqualTo(index));

            var remove = _panel.Book.WorldPointOfRemove(index);
            Assert.That(_panel.TryClick(remove), Is.True);
            _panel.Release(remove);

            Assert.That(_loop.State.Skills.Select(slot => slot.Skill), Has.None.SameAs(skill));
            Assert.That(_panel.Book.SelectedRow, Is.EqualTo(-1));
            Assert.That(_panel.Book.Hint, Is.EqualTo(BookTexts.Removed(skill)));
        }

        [Test]
        public void HalfHeightHidesTheBag()
        {
            Open();
            Assert.That(_panel.Book.IsCompact, Is.False);

            _navigator.ToggleHeight();
            Open();

            Assert.That(_panel.Book.IsCompact, Is.True);
            Assert.That(_panel.Book.Hint, Is.EqualTo(BookTexts.CompactHint));
        }

        private void Open()
        {
            _panel.Show(_navigator);
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();
        }
    }
}
