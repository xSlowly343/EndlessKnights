using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Math;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Core.Tests.Settings
{
    public class SettingsStoreTests
    {
        private string _directory = string.Empty;

        [SetUp]
        public void CreateDirectory() => _directory = Path.Combine(Path.GetTempPath(), "ek-settings-tests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveDirectory()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private static GameSettings Changed() => new GameSettings
        {
            FrameRate = 144,
            AlwaysOnTop = false,
            ClickThrough = false,
            ShowInputDebug = false,
            Paused = true,
            StripOffset = 312.5,
            StripScale = 1.5,
            NumberFormat = new NumberFormatSettings(NumberFormat.Engineering, 3, ",", "."),
            MasterVolume = 0.3,
            HitVolume = 0.2,
            CountWheel = false,
            Language = "en",
            InputConsentGiven = true
        };

        [Test]
        public void DefaultsMatchConcept()
        {
            var settings = new GameSettings();

            Assert.That(settings.FrameRate, Is.EqualTo(60));
            Assert.That(settings.HitVolume, Is.EqualTo(0));
            Assert.That(settings.NumberFormat.Format, Is.EqualTo(NumberFormat.Letters));
            Assert.That(settings.InputConsentGiven, Is.False);
            Assert.That(settings.CountKeys && settings.CountMouseMovement && settings.CountClicks && settings.CountWheel, Is.True);
        }

        [Test]
        public void WrittenSettingsLoadBack()
        {
            var store = new SettingsStore(_directory);
            store.Write(Changed());
            var problems = new List<string>();

            var loaded = store.Load(problems);

            Assert.That(problems, Is.Empty);
            Assert.That(SettingsCodec.Encode(loaded), Is.EqualTo(SettingsCodec.Encode(Changed())));
        }

        [Test]
        public void SettingsLiveInTheirOwnFile()
        {
            var store = new SettingsStore(_directory);

            store.Write(new GameSettings());

            Assert.That(Path.GetFileName(store.MainPath), Is.EqualTo("settings.json"));
            Assert.That(File.Exists(Path.Combine(_directory, "save.json")), Is.False);
        }

        [Test]
        public void NoFileGivesDefaults()
        {
            var problems = new List<string>();

            var loaded = new SettingsStore(_directory).Load(problems);

            Assert.That(problems, Is.Empty);
            Assert.That(SettingsCodec.Encode(loaded), Is.EqualTo(SettingsCodec.Encode(new GameSettings())));
        }

        [Test]
        public void BadValueFallsBackToItsDefaultOnly()
        {
            var text = SettingsCodec.Encode(Changed()).Replace("\"frameRate\": 144", "\"frameRate\": 75").Replace("\"master\": 0.3", "\"master\": 7");
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode(text, problems);

            Assert.That(loaded.FrameRate, Is.EqualTo(60));
            Assert.That(loaded.MasterVolume, Is.EqualTo(new GameSettings().MasterVolume));
            Assert.That(loaded.Language, Is.EqualTo("en"));
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems[0], Does.Contain("settings.json: frameRate: 75 is not one of 30, 60, 144"));
        }

        [Test]
        public void MissingFieldsKeepDefaults()
        {
            var problems = new List<string>();

            var loaded = SettingsCodec.Decode("{ \"version\": 1, \"language\": \"en\" }", problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Language, Is.EqualTo("en"));
            Assert.That(loaded.FrameRate, Is.EqualTo(60));
        }

        [Test]
        public void BrokenMainFileFallsBackToBackup()
        {
            var store = new SettingsStore(_directory);
            store.Write(Changed());
            store.Write(new GameSettings { Language = "en" });
            File.WriteAllText(store.MainPath, "{ broken");
            var problems = new List<string>();

            var loaded = store.Load(problems);

            Assert.That(loaded.FrameRate, Is.EqualTo(144));
            Assert.That(problems, Is.Not.Empty);
        }
    }
}
