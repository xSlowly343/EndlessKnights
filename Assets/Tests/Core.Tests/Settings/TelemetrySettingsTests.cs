using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Settings
{
    public class TelemetrySettingsTests
    {
        private const string SettingsBeforeTelemetry = @"{
  ""alwaysOnTop"": true,
  ""clickThrough"": true,
  ""decimalSeparator"": ""."",
  ""decimals"": 2,
  ""frameRate"": 60,
  ""input"": {
    ""clicks"": true,
    ""consentGiven"": true,
    ""keys"": true,
    ""mouseMovement"": true,
    ""wheel"": true
  },
  ""language"": ""ru"",
  ""numberFormat"": ""letters"",
  ""paused"": false,
  ""showInputDebug"": true,
  ""stripOffset"": 0,
  ""stripScale"": 1,
  ""thousandsSeparator"": "" "",
  ""version"": 1,
  ""volume"": {
    ""hits"": 0,
    ""interface"": 0.6,
    ""master"": 0.8,
    ""music"": 0.5,
    ""notifications"": 0.6,
    ""skills"": 0.6
  }
}";

        [Test]
        public void TelemetryIsOffByDefault()
        {
            Assert.That(new GameSettings().TelemetryConsentGiven, Is.False);
        }

        [Test]
        public void SettingsWrittenBeforeTelemetryStillLoadWithTelemetryOff()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode(SettingsBeforeTelemetry, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.InputConsentGiven, Is.True);
            Assert.That(loaded.TelemetryConsentGiven, Is.False);
        }

        [Test]
        public void TelemetryConsentIsSavedSeparatelyFromInputConsent()
        {
            var problems = new List<string>();
            var text = SettingsCodec.Encode(new GameSettings { TelemetryConsentGiven = true });

            var loaded = SettingsCodec.Decode(text, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.TelemetryConsentGiven, Is.True);
            Assert.That(loaded.InputConsentGiven, Is.False);
            Assert.That(text, Does.Contain("\"privacy\""));
        }

        [Test]
        public void BrokenTelemetryFlagFallsBackToOff()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode("{ \"version\": 1, \"privacy\": { \"telemetry\": \"yes\" } }", problems);

            Assert.That(loaded.TelemetryConsentGiven, Is.False);
            Assert.That(problems.Count, Is.EqualTo(1));
        }

        [Test]
        public void PrivacyTabTogglesTelemetry()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.Rows(2), Does.Contain(SettingId.Telemetry));
            Assert.That(SettingsScreen.KindOf(SettingId.Telemetry), Is.EqualTo(SettingKind.Toggle));
            Assert.That(SettingsScreen.Value(SettingId.Telemetry, settings), Is.EqualTo("Выкл"));
            Assert.That(SettingsScreen.Change(SettingId.Telemetry, settings, 1), Is.True);
            Assert.That(settings.TelemetryConsentGiven, Is.True);
            Assert.That(PanelSections.SubTabs(PanelSection.Settings)[2], Is.EqualTo("Приватность"));
            Assert.That(SettingsScreen.Note(2), Is.Not.Empty);
        }
    }
}
