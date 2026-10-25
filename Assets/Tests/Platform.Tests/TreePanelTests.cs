using System.IO;
using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class TreePanelTests
    {
        private PanelCanvas _panel = null!;
        private GameLoop _loop = null!;
        private GameData _data = null!;
        private PanelNavigator _navigator = null!;

        [SetUp]
        public void SetUp()
        {
            _data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            _loop = new GameLoop(_data, new FakeInputSource(), new GameState { Class = HeroClass.Viking }, new StatSheet());
            _loop.State.Tree.PointsEarned = 2;
            _panel = PanelCanvas.Create();
            _panel.Bind(_loop, _data.Tree);
            _navigator = new PanelNavigator();
            _navigator.SelectTab(PanelSection.Tree);
            Open();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void ClickingAnOfferedNodeShowsTakeAndTakingSpendsAPoint()
        {
            var node = _loop.State.Tree.Allocatable(_data.Tree).First(def => def.Type != Game.Core.Content.Tree.TreeNodeType.Attribute);
            Assert.That(_panel.Tree.LookAt(node), Is.EqualTo(TreeNodeLook.Offered));

            var point = _panel.Tree.WorldPointOf(node);
            Assert.That(_panel.TryClick(point), Is.True);
            _panel.Release(point);

            Assert.That(_panel.Tree.Selected, Is.SameAs(node));
            Assert.That(_panel.Tree.ActionCount, Is.EqualTo(1));

            Assert.That(_panel.TryClick(_panel.Tree.WorldPointOfAction(0)), Is.True);
            _panel.Release(_panel.Tree.WorldPointOfAction(0));

            Assert.That(_loop.State.Tree.IsAllocated(_data.Tree, node.Id), Is.True);
            Assert.That(_panel.Tree.LookAt(node), Is.EqualTo(TreeNodeLook.Taken));
            Assert.That(_panel.Tree.Points, Does.Contain(TreeTexts.Points(1)));
        }

        [Test]
        public void DraggingMovesTheTreeWithThePointerAndSelectsNothing()
        {
            var start = _data.Tree.StartOf(_loop.State.Tree.StartClass)!;
            var before = _panel.Tree.WorldPointOf(start);
            var shift = new Vector2(1.5f, -0.5f);

            _panel.TryClick(before);
            _panel.Drag(before + shift / 2f);
            _panel.Drag(before + shift);
            _panel.Release(before + shift);

            Assert.That(Vector2.Distance(_panel.Tree.WorldPointOf(start), before + shift), Is.LessThan(0.05f));
            Assert.That(_panel.Tree.Selected, Is.Null);
        }

        [Test]
        public void ZoomButtonInsideTheFieldZoomsIn()
        {
            var height = _panel.Tree.VisibleHeight;

            Assert.That(_panel.TryClick(_panel.Tree.WorldPointOfZoomIn()), Is.True);

            Assert.That(_panel.Tree.VisibleHeight, Is.LessThan(height));
        }

        [Test]
        public void ClickingANodeHighlightsItsGlyphAndClickingItAgainClearsIt()
        {
            var node = _loop.State.Tree.Allocatable(_data.Tree).Where(def => def.Type == Game.Core.Content.Tree.TreeNodeType.Small).OrderByDescending(def => def.Y).First();
            var point = _panel.Tree.WorldPointOf(node);

            _panel.TryClick(point);
            _panel.Release(point);

            Assert.That(_panel.Tree.Highlight, Is.EqualTo(Game.Core.Content.Tree.TreeGlyph.Of(node)));

            _panel.TryClick(point);
            _panel.Release(point);

            Assert.That(_panel.Tree.Selected, Is.Null);
            Assert.That(_panel.Tree.Highlight, Is.Empty);
        }

        [Test]
        public void FullyZoomedOutGlyphMarkersStayOnTheTree()
        {
            for (var i = 0; i < 20; i++)
                _panel.Tree.ZoomOut();

            var shown = _data.Tree.Nodes.Count(_panel.Tree.IsMarkerShown);

            Assert.That(shown, Is.GreaterThan(100));
            Assert.That(_data.Tree.Nodes.Where(node => node.Type == Game.Core.Content.Tree.TreeNodeType.Start).All(_panel.Tree.IsMarkerShown), Is.True);
        }

        [Test]
        public void HalfHeightKeepsTheCardShort()
        {
            Assert.That(_panel.Tree.IsCompact, Is.False);

            _navigator.ToggleHeight();
            Open();

            Assert.That(_panel.Tree.IsShown, Is.True);
            Assert.That(_panel.Tree.IsCompact, Is.True);
        }

        private void Open()
        {
            _panel.Show(_navigator);
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();
        }
    }
}
