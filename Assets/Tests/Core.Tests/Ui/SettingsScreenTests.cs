using System;
using System.Linq;
using Game.Core.Math;
using Game.Core.Settings;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class SettingsScreenTests
    {
        [Test]
        public void EverySettingIsOnExactlyOneTabAndHasALabel()
        {
            var shown = Enumerable.Range(0, SettingsScreen.TabCount).SelectMany(SettingsScreen.Rows).ToList();
            var all = (SettingId[])Enum.GetValues(typeof(SettingId));

            Assert.That(shown, Is.EquivalentTo(all));
            foreach (var id in all)
                Assert.That(Texts.Has("settings." + char.ToLowerInvariant(id.ToString()[0]) + id.ToString().Substring(1)), Is.True, id.ToString());
            Assert.That(PanelSections.SubTabs(PanelSection.Settings).Count, Is.EqualTo(SettingsScreen.TabCount));
        }

        [Test]
        public void TogglesFlipAndShowOnOff()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.Value(SettingId.AlwaysOnTop, settings), Is.EqualTo("Вкл"));
            Assert.That(SettingsScreen.Change(SettingId.AlwaysOnTop, settings, 1), Is.True);
            Assert.That(settings.AlwaysOnTop, Is.False);
            Assert.That(SettingsScreen.Value(SettingId.AlwaysOnTop, settings), Is.EqualTo("Выкл"));
        }

        [Test]
        public void ChoicesCycleBothWays()
        {
            var settings = new GameSettings();

            SettingsScreen.Change(SettingId.FrameRate, settings, 1);
            Assert.That(settings.FrameRate, Is.EqualTo(144));
            SettingsScreen.Change(SettingId.FrameRate, settings, 1);
            Assert.That(settings.FrameRate, Is.EqualTo(30));
            SettingsScreen.Change(SettingId.FrameRate, settings, -1);
            Assert.That(settings.FrameRate, Is.EqualTo(144));

            SettingsScreen.Change(SettingId.Language, settings, 1);
            Assert.That(settings.Language, Is.EqualTo("en"));
            SettingsScreen.Change(SettingId.NumberFormat, settings, -1);
            Assert.That(settings.NumberFormat.Format, Is.EqualTo(NumberFormat.Full));
        }

        [Test]
        public void VolumeMovesInTenPercentStepsAndStopsAtTheEnds()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings { MasterVolume = 0.9 };

            Assert.That(SettingsScreen.Change(SettingId.MasterVolume, settings, 1), Is.True);
            Assert.That(settings.MasterVolume, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(SettingsScreen.Change(SettingId.MasterVolume, settings, 1), Is.False);
            SettingsScreen.Change(SettingId.MasterVolume, settings, -1);
            Assert.That(SettingsScreen.Value(SettingId.MasterVolume, settings), Is.EqualTo("90%"));
        }

        [Test]
        public void DecimalsStayWithinTheFormatterRange()
        {
            var settings = new GameSettings();
            for (var i = 0; i < 5; i++)
                SettingsScreen.Change(SettingId.Decimals, settings, 1);

            Assert.That(settings.NumberFormat.Decimals, Is.EqualTo(NumberFormatSettings.MaxDecimals));
            Assert.That(SettingsScreen.Change(SettingId.Decimals, settings, 1), Is.False);
        }

        [Test]
        public void ResetPositionPutsTheStripBackAtTheStart()
        {
            var settings = new GameSettings { StripOffset = 320 };

            Assert.That(SettingsScreen.KindOf(SettingId.ResetPosition), Is.EqualTo(SettingKind.Action));
            SettingsScreen.Change(SettingId.ResetPosition, settings, 1);

            Assert.That(settings.StripOffset, Is.EqualTo(0));
        }

        [Test]
        public void ConsentRowOnlyOpensTheConsentScreen()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.Rows(2), Does.Contain(SettingId.InputConsent));
            Assert.That(SettingsScreen.KindOf(SettingId.InputConsent), Is.EqualTo(SettingKind.Action));
            Assert.That(SettingsScreen.Value(SettingId.InputConsent, settings), Is.EqualTo("Открыть"));
            Assert.That(SettingsScreen.Change(SettingId.InputConsent, settings, 1), Is.True);
            Assert.That(settings.InputConsentGiven, Is.False);
        }

        [Test]
        public void WhatsNewAndVersionTexts()
        {
            Texts.UseLanguage("ru");

            Assert.That(SettingsScreen.Rows(0), Does.Contain(SettingId.WhatsNew));
            Assert.That(SettingsScreen.Value(SettingId.WhatsNew, new GameSettings()), Is.EqualTo("Открыть"));
            Assert.That(SettingsScreen.Value(SettingId.ResetPosition, new GameSettings()), Is.EqualTo("Сбросить"));
            Assert.That(SettingsScreen.WhatsNewTitle("1.0"), Is.EqualTo("Что нового в 1.0"));
            Assert.That(SettingsScreen.Version("1.0"), Is.EqualTo("Rift Warden 1.0"));
        }

        [TearDown]
        public void RestoreLanguage() => Texts.UseLanguage("ru");
    }
}
