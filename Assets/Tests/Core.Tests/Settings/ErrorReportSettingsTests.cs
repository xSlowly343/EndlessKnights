using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Settings
{
    public class ErrorReportSettingsTests
    {
        private const string SettingsBeforeErrorReports = @"{
  ""alwaysOnTop"": false,
  ""frameRate"": 144,
  ""input"": {
    ""clicks"": true,
    ""consentGiven"": true,
    ""keys"": true,
    ""mouseMovement"": true,
    ""wheel"": true
  },
  ""language"": ""en"",
  ""version"": 1
}";

        [Test]
        public void ErrorReportsAreOffByDefault()
        {
            Assert.That(new GameSettings().ErrorReportConsentGiven, Is.False);
        }

        [Test]
        public void SettingsWrittenBeforeErrorReportsStillLoad()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode(SettingsBeforeErrorReports, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.FrameRate, Is.EqualTo(144));
            Assert.That(loaded.InputConsentGiven, Is.True);
            Assert.That(loaded.ErrorReportConsentGiven, Is.False);
        }

        [Test]
        public void ErrorReportConsentRoundTrips()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode(SettingsCodec.Encode(new GameSettings { ErrorReportConsentGiven = true }), problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.ErrorReportConsentGiven, Is.True);
            Assert.That(loaded.InputConsentGiven, Is.False);
        }

        [Test]
        public void BrokenFlagFallsBackToOff()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode("{ \"version\": 1, \"privacy\": { \"errorReports\": 1 } }", problems);

            Assert.That(loaded.ErrorReportConsentGiven, Is.False);
            Assert.That(problems.Count, Is.EqualTo(1));
        }

        [Test]
        public void PrivacyTabTogglesErrorReports()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.Rows(2), Does.Contain(SettingId.ErrorReports));
            Assert.That(SettingsScreen.Value(SettingId.ErrorReports, settings), Is.EqualTo("Выкл"));
            Assert.That(SettingsScreen.Change(SettingId.ErrorReports, settings, 1), Is.True);
            Assert.That(settings.ErrorReportConsentGiven, Is.True);
            Assert.That(SettingsScreen.Label(SettingId.ErrorReports), Is.EqualTo("Отправлять отчёты об ошибках"));
            Assert.That(PanelSections.SubTabs(PanelSection.Settings)[2], Is.EqualTo("Приватность"));
        }
    }
}
