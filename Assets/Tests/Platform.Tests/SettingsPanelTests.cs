using Game.Core.Settings;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class SettingsPanelTests
    {
        private PanelCanvas _panel = null!;
        private PanelNavigator _navigator = null!;
        private GameSettings _settings = null!;

        [SetUp]
        public void SetUp()
        {
            Texts.UseLanguage("ru");
            _settings = new GameSettings();
            _panel = PanelCanvas.Create();
            _panel.Settings.Use(_settings);
            _navigator = new PanelNavigator();
            _navigator.SelectTab(PanelSection.Settings);
            Open();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void GameTabListsItsRows()
        {
            Assert.That(_panel.Settings.IsShown, Is.True);
            Assert.That(_panel.Settings.ShownRows, Is.EqualTo(SettingsScreen.Rows(0).Count));
        }

        [Test]
        public void ClickingAToggleFlipsTheSettingAndReportsIt()
        {
            var row = IndexOf(SettingId.AlwaysOnTop, 0);
            SettingId? changed = null;
            _panel.Settings.Changed += id => changed = id;

            Assert.That(_panel.TryClick(_panel.Settings.WorldPointOfControl(row)), Is.True);

            Assert.That(_settings.AlwaysOnTop, Is.False);
            Assert.That(changed, Is.EqualTo(SettingId.AlwaysOnTop));
            Assert.That(_panel.Settings.ValueOf(row), Is.EqualTo("Выкл"));
        }

        [Test]
        public void VersionSitsInTheCorner()
        {
            _panel.Settings.UseVersion("1.0");

            Assert.That(_panel.Settings.VersionText, Is.EqualTo("Rift Warden 1.0"));
        }

        [Test]
        public void ArrowsStepAChoice()
        {
            var row = IndexOf(SettingId.FrameRate, 0);

            _panel.TryClick(_panel.Settings.WorldPointOfNext(row));
            Assert.That(_settings.FrameRate, Is.EqualTo(144));

            _panel.TryClick(_panel.Settings.WorldPointOfPrevious(row));
            Assert.That(_settings.FrameRate, Is.EqualTo(60));
        }

        [Test]
        public void SoundTabShowsTheVolumes()
        {
            _navigator.SelectSubTab(1);
            Open();

            Assert.That(_panel.Settings.ShownRows, Is.EqualTo(SettingsScreen.Rows(1).Count));
            var row = IndexOf(SettingId.MasterVolume, 1);
            _panel.TryClick(_panel.Settings.WorldPointOfPrevious(row));
            Assert.That(_settings.MasterVolume, Is.EqualTo(0.7).Within(1e-9));
        }

        private static int IndexOf(SettingId id, int tab)
        {
            var rows = SettingsScreen.Rows(tab);
            for (var i = 0; i < rows.Count; i++)
                if (rows[i] == id)
                    return i;
            return -1;
        }

        private void Open()
        {
            _panel.Show(_navigator);
            _panel.Place(0f, true);
            Canvas.ForceUpdateCanvases();
        }
    }
}
