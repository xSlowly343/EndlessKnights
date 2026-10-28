using System.Collections.Generic;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Core.Tests.Settings
{
    public class PrivacyPolicyTests
    {
        private const string SettingsBeforePolicy = @"{
  ""input"": {
    ""clicks"": true,
    ""consentGiven"": true,
    ""keys"": true,
    ""mouseMovement"": false,
    ""wheel"": true
  },
  ""language"": ""ru"",
  ""version"": 1
}";

        [Test]
        public void FreshInstallIsAskedAndHasAcceptedNothing()
        {
            var settings = new GameSettings();

            Assert.That(settings.AcceptedPolicyVersion, Is.EqualTo(0));
            Assert.That(PrivacyPolicy.NeedsAcceptance(settings), Is.True);
            Assert.That(PrivacyPolicy.ShouldAskConsent(settings), Is.True);
            Assert.That(PrivacyPolicy.WasUpdated(settings), Is.False);
        }

        [Test]
        public void AcceptingGivesConsentAndRemembersTheVersion()
        {
            var settings = new GameSettings();

            PrivacyPolicy.Accept(settings);

            Assert.That(settings.InputConsentGiven, Is.True);
            Assert.That(settings.AcceptedPolicyVersion, Is.EqualTo(PrivacyPolicy.CurrentVersion));
            Assert.That(PrivacyPolicy.ShouldAskConsent(settings), Is.False);
        }

        [Test]
        public void NewPolicyVersionAsksAgainWithoutStoppingInput()
        {
            var settings = new GameSettings();
            PrivacyPolicy.Accept(settings, 1);

            Assert.That(PrivacyPolicy.ShouldAskConsent(settings, 2), Is.True);
            Assert.That(PrivacyPolicy.WasUpdated(settings, 2), Is.True);
            Assert.That(InputPermissions.From(settings).Consent, Is.True);
            PrivacyPolicy.Accept(settings, 2);
            Assert.That(settings.AcceptedPolicyVersion, Is.EqualTo(2));
            Assert.That(PrivacyPolicy.ShouldAskConsent(settings, 2), Is.False);
        }

        [Test]
        public void AcceptingNeverLowersTheVersion()
        {
            var settings = new GameSettings { AcceptedPolicyVersion = 5 };

            PrivacyPolicy.Accept(settings, 1);

            Assert.That(settings.AcceptedPolicyVersion, Is.EqualTo(5));
        }

        [Test]
        public void SettingsFromBeforeThePolicyKeepInputConsentButAskForThePolicy()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode(SettingsBeforePolicy, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.InputConsentGiven, Is.True);
            Assert.That(loaded.CountMouseMovement, Is.False);
            Assert.That(loaded.AcceptedPolicyVersion, Is.EqualTo(0));
            Assert.That(PrivacyPolicy.WasUpdated(loaded), Is.True);
        }

        [Test]
        public void AcceptedVersionIsSavedAndLoaded()
        {
            var settings = new GameSettings();
            PrivacyPolicy.Accept(settings);
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode(SettingsCodec.Encode(settings), problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.AcceptedPolicyVersion, Is.EqualTo(PrivacyPolicy.CurrentVersion));
            Assert.That(PrivacyPolicy.ShouldAskConsent(loaded), Is.False);
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("\"one\"")]
        public void BrokenVersionMeansNotAccepted(string value)
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode("{ \"version\": 1, \"privacy\": { \"policyVersion\": " + value + " } }", problems);

            Assert.That(loaded.AcceptedPolicyVersion, Is.EqualTo(0));
            Assert.That(problems.Count, Is.EqualTo(1));
        }
    }
}
