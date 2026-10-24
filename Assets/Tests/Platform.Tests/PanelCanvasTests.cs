using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class PanelCanvasTests
    {
        private PanelCanvas _panel = null!;
        private PanelNavigator _navigator = null!;

        [SetUp]
        public void SetUp()
        {
            _panel = PanelCanvas.Create();
            _navigator = new PanelNavigator();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void OpenPanelTakesTheFullFrame()
        {
            Open(PanelSection.Hero);

            Assert.That(_panel.CanvasRect.sizeDelta, Is.EqualTo(new Vector2(PanelGeometry.WidthPx, PanelGeometry.FullHeightPx)));
        }

        [Test]
        public void HalfHeightShrinksTheCanvas()
        {
            Open(PanelSection.Hero);
            _navigator.ToggleHeight();
            Show();

            Assert.That(_panel.HeightPx, Is.EqualTo(PanelGeometry.HalfHeightPx));
        }

        [Test]
        public void CloseButtonInTheTopRightCornerClosesThePanel()
        {
            var closed = false;
            _panel.ClosePressed += () => closed = true;
            Open(PanelSection.Hero);

            var handled = _panel.TryClick(PointAt(PanelGeometry.WidthPx - 24f, PanelGeometry.FullHeightPx - 24f));

            Assert.That(handled, Is.True);
            Assert.That(closed, Is.True);
        }

        [Test]
        public void ThirdTabOpensTheTree()
        {
            PanelSection? pressed = null;
            _panel.TabPressed += section => pressed = section;
            Open(PanelSection.Hero);

            _panel.TryClick(PointAt(PanelGeometry.WidthPx / (float)PanelCanvas.TabCount * 2.5f, PanelGeometry.TabBarPx / 2f));

            Assert.That(pressed, Is.EqualTo(PanelSection.Tree));
        }

        [Test]
        public void LastTabOpensMore()
        {
            var more = false;
            _panel.MorePressed += () => more = true;
            Open(PanelSection.Hero);

            _panel.TryClick(PointAt(PanelGeometry.WidthPx - 20f, PanelGeometry.TabBarPx / 2f));

            Assert.That(more, Is.True);
        }

        [Test]
        public void ClickOutsideThePanelIsNotHandled()
        {
            Open(PanelSection.Hero);

            Assert.That(_panel.TryClick(PointAt(PanelGeometry.WidthPx / 2f, PanelGeometry.FullHeightPx + 40f)), Is.False);
        }

        [Test]
        public void ReportUsesHalfHeightAndClaimButton()
        {
            var claimed = false;
            _panel.ClaimPressed += () => claimed = true;
            _panel.ShowReport("Пока тебя не было", new[] { "Золото: +10" });
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();

            _panel.TryClick(PointAt(PanelGeometry.WidthPx / 2f, 26f));

            Assert.That(_panel.HeightPx, Is.EqualTo(PanelGeometry.HalfHeightPx));
            Assert.That(claimed, Is.True);
        }

        private void Open(PanelSection section)
        {
            _navigator.SelectTab(section);
            Show();
        }

        private void Show()
        {
            _panel.Show(_navigator);
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();
        }

        private Vector2 PointAt(float xPx, float yPx) => _panel.CanvasRect.TransformPoint(new Vector3(xPx, yPx, 0f));
    }
}
