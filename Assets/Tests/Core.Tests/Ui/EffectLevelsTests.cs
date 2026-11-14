using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class EffectLevelsTests
    {
        private static EffectLevels At(double world, bool saving = false) => EffectLevels.Of(new GameSettings { LivingWorld = world }, saving);

        [Test]
        public void ByDefaultEverythingIsShownInFull()
        {
            var levels = EffectLevels.Of(new GameSettings(), false);

            Assert.That(levels.World, Is.EqualTo(1));
            Assert.That(levels.Battle, Is.EqualTo(1));
            Assert.That(levels.WeatherLayers, Is.EqualTo(3));
            Assert.That(levels.BackgroundCreatures, Is.EqualTo(6));
            Assert.That(levels.GroundCreatures, Is.EqualTo(3));
            Assert.That(levels.DayNight && levels.Reactions && levels.RareScenes, Is.True);
            Assert.That(levels.CpuPercent, Is.EqualTo(6).Within(1e-9), "the whole strip stays within the 6% budget");
        }

        [Test]
        public void TheSliderScalesLayersAndCreaturesAndGatesReactionsAndScenes()
        {
            Assert.That(At(0).WeatherLayers, Is.EqualTo(0));
            Assert.That(At(0).WorldShown, Is.False);
            Assert.That(At(0.05).WeatherLayers, Is.EqualTo(1));
            Assert.That(At(0.5).WeatherLayers, Is.EqualTo(1));
            Assert.That(At(0.55).WeatherLayers, Is.EqualTo(2));
            Assert.That(At(0.8).WeatherLayers, Is.EqualTo(2));
            Assert.That(At(0.85).WeatherLayers, Is.EqualTo(3));

            Assert.That(At(0.5).BackgroundCreatures, Is.EqualTo(3));
            Assert.That(At(0.5).GroundCreatures, Is.EqualTo(1));
            Assert.That(At(0.3).BackgroundCreatures, Is.EqualTo(1));
            Assert.That(At(0.3).GroundCreatures, Is.EqualTo(0));

            Assert.That(At(0.2).Reactions, Is.False);
            Assert.That(At(0.25).Reactions, Is.True);
            Assert.That(At(0.45).RareScenes, Is.False);
            Assert.That(At(0.5).RareScenes, Is.True);
        }

        [Test]
        public void SwitchesTurnTheirPartOffWhateverTheSlider()
        {
            var settings = new GameSettings { WeatherShown = false, WildlifeShown = false, WorldReactions = false, RareScenes = false, DayNightShown = false };
            var levels = EffectLevels.Of(settings, false);

            Assert.That(levels.WeatherLayers, Is.EqualTo(0));
            Assert.That(levels.BackgroundCreatures + levels.GroundCreatures, Is.EqualTo(0));
            Assert.That(levels.Reactions || levels.RareScenes || levels.DayNight, Is.False);
            Assert.That(levels.WorldShown, Is.False);
            Assert.That(levels.CpuPercent, Is.EqualTo(4).Within(1e-9));
        }

        [Test]
        public void SavingPowerHoldsTheSlidersAtHalfUnlessTheSaverIsOff()
        {
            var saved = At(1, saving: true);
            Assert.That(saved.World, Is.EqualTo(0.5));
            Assert.That(saved.Battle, Is.EqualTo(0.5));
            Assert.That(saved.WeatherLayers, Is.EqualTo(1));
            Assert.That(At(0.3, saving: true).World, Is.EqualTo(0.3), "a lower choice is kept");

            var off = EffectLevels.Of(new GameSettings { BatterySaver = false }, true);
            Assert.That(off.World, Is.EqualTo(1));
        }

        [Test]
        public void TheLoadEstimateFallsWithTheSliders()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();
            var full = EffectLevels.Of(settings, false).CpuPercent;
            settings.LivingWorld = 0.5;
            var half = EffectLevels.Of(settings, false).CpuPercent;
            settings.LivingWorld = 0;
            settings.BattleEffects = 0;
            var none = EffectLevels.Of(settings, false).CpuPercent;

            Assert.That(half, Is.LessThan(full));
            Assert.That(none, Is.EqualTo(EffectLevels.StripCpuPercent).Within(1e-9));
            Assert.That(SettingsScreen.LoadLine(new GameSettings(), false), Is.EqualTo("≈ 6.0% CPU"));
        }

        [Test]
        public void TheEffectsTabHoldsTheWorldTheStrikesAndThePowerSwitch()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();
            var rows = SettingsScreen.Rows(SettingsScreen.EffectsTab);

            Assert.That(rows, Is.EqualTo(new[]
            {
                SettingId.LivingWorld, SettingId.WeatherShown, SettingId.DayNightShown, SettingId.WildlifeShown, SettingId.WorldReactions, SettingId.RareScenes,
                SettingId.HeroStrikeOpacity, SettingId.AllyStrikeOpacity, SettingId.BattleEffects, SettingId.AuraShow, SettingId.CompanionStrip,
                SettingId.BatterySaver
            }));
            Assert.That(SettingsScreen.KindOf(SettingId.LivingWorld), Is.EqualTo(SettingKind.Slider));
            Assert.That(SettingsScreen.KindOf(SettingId.WeatherShown), Is.EqualTo(SettingKind.Toggle));
            Assert.That(SettingsScreen.Value(SettingId.LivingWorld, settings), Is.EqualTo("100%"));
            Assert.That(SettingsScreen.SetFraction(SettingId.LivingWorld, settings, 0.52), Is.True);
            Assert.That(settings.LivingWorld, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(SettingsScreen.Change(SettingId.RareScenes, settings, 1), Is.True);
            Assert.That(settings.RareScenes, Is.False);
            foreach (var id in rows)
            {
                Assert.That(SettingsScreen.Label(id), Does.Not.StartWith("settings."), id.ToString());
                Assert.That(SettingsScreen.Hint(id), Does.Not.StartWith("settings."), id.ToString());
            }

            var changed = new List<SettingId>();
            SettingsScreen.ResetTab(SettingsScreen.EffectsTab, settings, changed);
            Assert.That(changed, Is.EquivalentTo(new[] { SettingId.LivingWorld, SettingId.RareScenes }));
            Assert.That(settings.LivingWorld, Is.EqualTo(1));
            Assert.That(SettingsScreen.Note(SettingsScreen.EffectsTab), Does.Contain("отряда"));
        }

        [Test]
        public void EffectSettingsSurviveSavingAndOldFilesKeepDefaults()
        {
            var settings = new GameSettings { LivingWorld = 0.35, BattleEffects = 0.6, WeatherShown = false, DayNightShown = false, WildlifeShown = false, WorldReactions = false, RareScenes = false, BatterySaver = false };
            var problems = new List<string>();
            var read = SettingsCodec.Decode(SettingsCodec.Encode(settings), problems);

            Assert.That(problems, Is.Empty);
            Assert.That(read.LivingWorld, Is.EqualTo(0.35));
            Assert.That(read.BattleEffects, Is.EqualTo(0.6));
            Assert.That(read.WeatherShown || read.DayNightShown || read.WildlifeShown || read.WorldReactions || read.RareScenes || read.BatterySaver, Is.False);

            var old = SettingsCodec.Decode("{\"version\": 1, \"frameRate\": 60}", problems);
            Assert.That(old.LivingWorld, Is.EqualTo(1));
            Assert.That(old.WeatherShown && old.BatterySaver, Is.True);

            SettingsCodec.Decode("{\"effects\": {\"livingWorld\": 7}}", problems);
            Assert.That(problems, Has.Count.EqualTo(1));
        }
    }
}
