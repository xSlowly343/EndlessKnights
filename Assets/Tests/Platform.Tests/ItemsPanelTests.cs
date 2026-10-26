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
    public class ItemsPanelTests
    {
        private PanelCanvas _panel = null!;
        private GameLoop _loop = null!;

        [SetUp]
        public void SetUp()
        {
            var data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            _loop = new GameLoop(data, new FakeInputSource(), new GameState { Class = HeroClass.Viking, RecordStage = 500 }, new StatSheet());
            _panel = PanelCanvas.Create();
            _panel.Items.Bind(_loop);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void RightHandButtonEquipsTheSelectedWeapon()
        {
            var starter = _loop.Weapons.In(WeaponHand.Right)!;
            var other = _loop.Weapons.Give(_loop.Data.Weapons.Bases.First(weapon => weapon.Type != starter.Type));
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Items);
            _panel.Show(navigator);
            _panel.Place(0f, true);
            _panel.Items.Select(other);
            Canvas.ForceUpdateCanvases();

            _panel.TryClick(_panel.CanvasRect.TransformPoint(new Vector3(133f, 95f, 0f)));

            Assert.That(_loop.Weapons.In(WeaponHand.Right), Is.SameAs(other));
            Assert.That(_loop.Weapons.Bag, Does.Contain(starter));
            Assert.That(_panel.Items.Selected, Is.Null);
        }

        [Test]
        public void ClickOnAFilledCellSelectsTheWeapon()
        {
            var weapon = _loop.Weapons.Give(_loop.Data.Weapons.Bases.Last());
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Items);
            _panel.Show(navigator);
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();

            _panel.TryClick(_panel.CanvasRect.TransformPoint(new Vector3(45f, 600f - 46f - 40f - 4f - 26f - 8f - 27f, 0f)));

            Assert.That(_panel.Items.Selected, Is.SameAs(weapon));
        }
    }
}
