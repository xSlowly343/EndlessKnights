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
            _navigator.SelectSubTab(1);
            Open();
            var row = IndexOf(SettingId.AlwaysOnTop, 1);
            SettingId? changed = null;
            _panel.Settings.Changed += id => changed = id;

            Assert.That(_panel.TryClick(_panel.Settings.WorldPointOfControl(row)), Is.True);

            Assert.That(_settings.AlwaysOnTop, Is.False);
            Assert.That(changed, Is.EqualTo(SettingId.AlwaysOnTop));
            Assert.That(_panel.Settings.ValueOf(row), Is.EqualTo("Выкл"));
            Assert.That(_panel.Settings.ChangedMarkOf(row), Is.True);
        }

        [Test]
        public void VersionSitsOnTheAboutCard()
        {
            _panel.Settings.UseVersion("1.0");

            Assert.That(_panel.Settings.VersionText, Is.EqualTo("Rift Warden 1.0"));
        }

        [Test]
        public void ArrowsStepAChoice()
        {
            var row = IndexOf(SettingId.Decimals, 0);
            var before = _settings.NumberFormat.Decimals;

            _panel.TryClick(_panel.Settings.WorldPointOfNext(row));
            Assert.That(_settings.NumberFormat.Decimals, Is.EqualTo(before + 1));

            _panel.TryClick(_panel.Settings.WorldPointOfPrevious(row));
            Assert.That(_settings.NumberFormat.Decimals, Is.EqualTo(before));
        }

        [Test]
        public void SegmentsPickTheClickedOption()
        {
            var row = IndexOf(SettingId.Language, 0);

            Assert.That(_panel.TryClick(_panel.Settings.WorldPointOfOption(row, 1)), Is.True);

            Assert.That(_settings.Language, Is.EqualTo("en"));
        }

        [Test]
        public void SoundTabSetsAVolumeByAClickOnTheSlider()
        {
            _navigator.SelectSubTab(2);
            Open();

            Assert.That(_panel.Settings.ShownRows, Is.EqualTo(SettingsScreen.Rows(2).Count));
            var row = IndexOf(SettingId.MasterVolume, 2);
            _panel.TryClick(_panel.Settings.WorldPointOfSlider(row, 0.25f));
            Assert.That(_settings.MasterVolume, Is.EqualTo(0.25).Within(1e-9));
            Assert.That(_panel.Settings.ValueOf(row), Is.EqualTo("25%"));
        }

        [Test]
        public void ResetButtonAppearsAfterAChangeAndPutsTheTabBack()
        {
            Assert.That(_panel.Settings.ResetShown, Is.False);
            var row = IndexOf(SettingId.Language, 0);
            _panel.TryClick(_panel.Settings.WorldPointOfOption(row, 1));
            Canvas.ForceUpdateCanvases();
            Assert.That(_panel.Settings.ResetShown, Is.True);

            Assert.That(_panel.TryClick(_panel.Settings.WorldPointOfReset), Is.True);

            Assert.That(_settings.Language, Is.EqualTo("ru"));
            Assert.That(_panel.Settings.ResetShown, Is.False);
        }

        [Test]
        public void TheNumberFormatRowShowsASample()
        {
            var row = IndexOf(SettingId.NumberFormat, 0);

            Assert.That(_panel.Settings.HintOf(row), Does.StartWith("Пример: "));
        }

        [Test]
        public void TheLookLivesInTheHeroSection()
        {
            _navigator.SelectTab(PanelSection.Hero);
            _navigator.SelectSubTab(2);
            Open();

            Assert.That(_panel.Settings.IsShown, Is.True);
            Assert.That(_panel.Settings.ShownRows, Is.EqualTo(SettingsScreen.Rows(SettingsScreen.LookTab).Count));
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
