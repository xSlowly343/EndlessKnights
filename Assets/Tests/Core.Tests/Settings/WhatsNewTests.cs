using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Core.Tests.Settings
{
    public class WhatsNewTests
    {
        private static ChangeLog Log(string game) => ChangeLog.Load("{ \"version\": 1, \"entries\": [{ \"game\": \"" + game + "\", \"lines\": { \"ru\": [\"Новое\"] } }] }");

        [Test]
        public void AnUpdateShowsWhatsNewOnceAndRemembersTheVersion()
        {
            var settings = new GameSettings { SeenChangeLog = "1.0" };

            Assert.That(WhatsNew.ShowOnStart(settings, Log("1.1"), firstRun: false), Is.True);
            Assert.That(settings.SeenChangeLog, Is.EqualTo("1.1"));
            Assert.That(WhatsNew.ShowOnStart(settings, Log("1.1"), firstRun: false), Is.False);
        }

        [Test]
        public void AFirstRunRemembersTheVersionWithoutShowingIt()
        {
            var settings = new GameSettings();

            Assert.That(WhatsNew.ShowOnStart(settings, Log("1.0"), firstRun: true), Is.False);
            Assert.That(settings.SeenChangeLog, Is.EqualTo("1.0"));
        }

        [Test]
        public void TheSeenVersionSurvivesTheSettingsFile()
        {
            var settings = new GameSettings { SeenChangeLog = "1.2" };

            var decoded = SettingsCodec.Decode(SettingsCodec.Encode(settings), new List<string>());

            Assert.That(decoded.SeenChangeLog, Is.EqualTo("1.2"));
        }
    }
}
