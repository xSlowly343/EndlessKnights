using System.Collections.Generic;
using Game.Core.Settings;
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
    }
}
