using System.IO;
using System.Linq;
using Game.Core.Build;
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
    public class SubclassPanelTests
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
            _loop.CompleteActivity(_data.Ascendancy.Activities[0].Id);
            _panel = PanelCanvas.Create();
            _panel.Bind(_loop, _data.Tree);
            _navigator = new PanelNavigator();
            _navigator.SelectTab(PanelSection.Tree);
            _navigator.SelectSubTab(1);
            Open();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void WithoutAPathTheTabOffersTheClassPaths()
        {
            Assert.That(_panel.Subclass.IsShown, Is.True);
            Assert.That(_panel.Subclass.IsChoosing, Is.True);
            Assert.That(_panel.Tree.IsShown, Is.False);
        }

        [Test]
        public void ChoosingAPathShowsItsTreeAndTakingANodeSpendsAPathPoint()
        {
            var offered = _data.Ascendancy.SubclassesOf(_loop.State.Tree.StartClass)[0];

            Assert.That(_panel.TryClick(_panel.Subclass.WorldPointOfChoice(0)), Is.True);

            Assert.That(_loop.State.Ascendancy.Subclass, Is.EqualTo(offered.Id));
            Assert.That(_panel.Subclass.IsChoosing, Is.False);

            var node = offered.Nodes.First(def => SubclassView.CanTake(_loop.State.Ascendancy, _data.Ascendancy, def));
            Assert.That(_panel.TryClick(_panel.Subclass.WorldPointOf(node)), Is.True);
            Assert.That(_panel.Subclass.Selected, Is.SameAs(node));
            Assert.That(_panel.Subclass.ActionCount, Is.EqualTo(1));
            Canvas.ForceUpdateCanvases();

            Assert.That(_panel.TryClick(_panel.Subclass.WorldPointOfAction(0)), Is.True);

            Assert.That(_loop.State.Ascendancy.IsAllocated(node.Id), Is.True);
            Assert.That(_loop.State.Ascendancy.PointsLeft(_data.Ascendancy), Is.EqualTo(0));
        }

        [Test]
        public void HoveringAPathNodeShowsItsTooltipAndPointingAwayHidesIt()
        {
            var offered = _data.Ascendancy.SubclassesOf(_loop.State.Tree.StartClass)[0];
            _loop.ChooseSubclass(offered.Id);
            Open();

            _panel.Point(_panel.Subclass.WorldPointOf(offered.Start), false);

            Assert.That(_panel.Subclass.Hovered, Is.SameAs(offered.Start));
            Assert.That(_panel.Subclass.IsTooltipShown, Is.True);

            _panel.PointAway();

            Assert.That(_panel.Subclass.IsTooltipShown, Is.False);
        }

        private void Open()
        {
            _panel.Show(_navigator);
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();
        }
    }
}
