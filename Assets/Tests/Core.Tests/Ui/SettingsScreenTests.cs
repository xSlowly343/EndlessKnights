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
            var shown = Enumerable.Range(0, SettingsScreen.TabCount + 1).SelectMany(SettingsScreen.Rows).ToList();
            var all = (SettingId[])Enum.GetValues(typeof(SettingId));
            var offScreen = new[] { SettingId.Paused, SettingId.ClickThrough, SettingId.CountKeys, SettingId.CountMouseMovement, SettingId.CountClicks, SettingId.CountWheel, SettingId.ShowInputDebug };

            Assert.That(shown.Concat(offScreen), Is.EquivalentTo(all));
            foreach (var id in all)
                Assert.That(Texts.Has("settings." + char.ToLowerInvariant(id.ToString()[0]) + id.ToString().Substring(1)), Is.True, id.ToString());
            Assert.That(PanelSections.SubTabs(PanelSection.Settings), Is.EqualTo(new[] { "Игра", "Окно", "Звук", "Об игре" }));
            Assert.That(PanelSections.SubTabs(PanelSection.Hero)[2], Is.EqualTo("Облик"));
            Assert.That(SettingsScreen.Rows(SettingsScreen.LookTab), Is.EqualTo(new[] { SettingId.HeroCloak, SettingId.HeroEyes, SettingId.HeroStrike }));
            foreach (var id in shown)
                Assert.That(Texts.Has("settings.hint." + char.ToLowerInvariant(id.ToString()[0]) + id.ToString().Substring(1)), Is.True, "hint of " + id);
            for (var tab = 0; tab <= SettingsScreen.LookTab; tab++)
                foreach (var group in SettingsScreen.Groups(tab))
                    Assert.That(Texts.Has("settings.group." + group.Key), Is.True, group.Key);
        }

        [Test]
        public void StrikeVisibilityIsSetApartForTheHeroAndTheParty()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.Rows(0), Does.Contain(SettingId.HeroStrikeOpacity).And.Contain(SettingId.AllyStrikeOpacity));
            Assert.That(SettingsScreen.Value(SettingId.HeroStrikeOpacity, settings), Is.EqualTo("100%"));
            Assert.That(SettingsScreen.Value(SettingId.AllyStrikeOpacity, settings), Is.EqualTo("50%"));
            Assert.That(SettingsScreen.Change(SettingId.HeroStrikeOpacity, settings, 1), Is.False);
            Assert.That(SettingsScreen.Change(SettingId.AllyStrikeOpacity, settings, -1), Is.True);
            Assert.That(settings.AllyStrikeOpacity, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(settings.HeroStrikeOpacity, Is.EqualTo(1.0).Within(1e-9));
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

            Assert.That(SettingsScreen.Rows(SettingsScreen.AboutTab), Does.Contain(SettingId.InputConsent));
            Assert.That(SettingsScreen.KindOf(SettingId.InputConsent), Is.EqualTo(SettingKind.Action));
            Assert.That(SettingsScreen.Value(SettingId.InputConsent, settings), Is.EqualTo("Открыть"));
            Assert.That(SettingsScreen.Change(SettingId.InputConsent, settings, 1), Is.True);
            Assert.That(settings.InputConsentGiven, Is.False);
        }

        [Test]
        public void WhatsNewAndVersionTexts()
        {
            Texts.UseLanguage("ru");

            Assert.That(SettingsScreen.Rows(SettingsScreen.AboutTab), Does.Contain(SettingId.WhatsNew));
            Assert.That(SettingsScreen.Value(SettingId.WhatsNew, new GameSettings()), Is.EqualTo("Открыть"));
            Assert.That(SettingsScreen.Value(SettingId.ResetPosition, new GameSettings()), Is.EqualTo("Сбросить"));
            Assert.That(SettingsScreen.WhatsNewTitle("1.0"), Is.EqualTo("Что нового в 1.0"));
            Assert.That(SettingsScreen.Version("1.0"), Is.EqualTo("Rift Warden 1.0"));
        }

        [Test]
        public void AboutTabCreditsTheIconAuthorsAndSoundTabExplainsSilence()
        {
            Texts.UseLanguage("ru");

            Assert.That(SettingsScreen.Note(SettingsScreen.AboutTab), Does.Contain("game-icons.net").And.Contain("CC BY 3.0").And.Contain("Lorc"));
            Assert.That(SettingsScreen.Note(2), Is.EqualTo(Texts.Get("settings.soundNote")));
            Assert.That(SettingsScreen.Note(SettingsScreen.LookTab), Is.EqualTo(Texts.Get("settings.lookNote")));
            Assert.That(SettingsScreen.Note(0), Is.Empty);
        }

        [Test]
        public void TheWindowTabHidesTheStripByKeyAndInFullscreenWindows()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();
            var rows = SettingsScreen.Rows(1);

            Assert.That(rows, Does.Contain(SettingId.AutoHideFullscreen).And.Contain(SettingId.HideHotKey));
            Assert.That(SettingsScreen.KindOf(SettingId.AutoHideFullscreen), Is.EqualTo(SettingKind.Toggle));
            Assert.That(settings.AutoHideFullscreen, Is.False, "the strip stays above borderless games until asked");
            Assert.That(settings.HideHotKey, Is.True);
            Assert.That(SettingsScreen.Label(SettingId.HideHotKey), Does.Contain("Ctrl+Alt+H"));
            Assert.That(SettingsScreen.Hint(SettingId.AutoHideFullscreen), Is.Not.Empty);

            Assert.That(SettingsScreen.Change(SettingId.AutoHideFullscreen, settings, 1), Is.True);
            Assert.That(SettingsScreen.Change(SettingId.HideHotKey, settings, 1), Is.True);
            Assert.That(settings.AutoHideFullscreen, Is.True);
            Assert.That(settings.HideHotKey, Is.False);
            Assert.That(SettingsScreen.AnyChanged(1, settings), Is.True);

            var changed = new System.Collections.Generic.List<SettingId>();
            SettingsScreen.ResetTab(1, settings, changed);
            Assert.That(changed, Does.Contain(SettingId.AutoHideFullscreen).And.Contain(SettingId.HideHotKey));
            Assert.That(settings.AutoHideFullscreen, Is.False);
            Assert.That(settings.HideHotKey, Is.True);
        }

        [Test]
        public void NothingAboutInputOrPauseIsLeftInTheMenu()
        {
            var shown = Enumerable.Range(0, SettingsScreen.TabCount + 1).SelectMany(SettingsScreen.Rows).ToList();

            Assert.That(shown, Has.None.EqualTo(SettingId.Paused));
            Assert.That(shown, Has.None.EqualTo(SettingId.CountKeys).And.None.EqualTo(SettingId.CountMouseMovement));
            Assert.That(shown, Has.None.EqualTo(SettingId.CountClicks).And.None.EqualTo(SettingId.CountWheel));
            var settings = new GameSettings();
            Assert.That(settings.CountKeys && settings.CountMouseMovement && settings.CountClicks && settings.CountWheel, Is.True);
        }

        [Test]
        public void SegmentsPickAnOptionDirectly()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.KindOf(SettingId.FrameRate), Is.EqualTo(SettingKind.Segments));
            Assert.That(SettingsScreen.OptionCount(SettingId.FrameRate), Is.EqualTo(3));
            Assert.That(SettingsScreen.OptionLabel(SettingId.FrameRate, 2), Is.EqualTo("144"));
            Assert.That(SettingsScreen.Selected(SettingId.FrameRate, settings), Is.EqualTo(1));
            Assert.That(SettingsScreen.Select(SettingId.FrameRate, settings, 1), Is.False, "already chosen");
            Assert.That(SettingsScreen.Select(SettingId.FrameRate, settings, 0), Is.True);
            Assert.That(settings.FrameRate, Is.EqualTo(30));
            Assert.That(SettingsScreen.Select(SettingId.FrameRate, settings, 3), Is.False);
            Assert.That(SettingsScreen.Select(SettingId.Language, settings, 1), Is.True);
            Assert.That(settings.Language, Is.EqualTo("en"));
            Assert.That(SettingsScreen.Select(SettingId.CompanionStrip, settings, 2), Is.True);
            Assert.That(settings.CompanionStrip, Is.EqualTo("off"));
            foreach (var id in new[] { SettingId.Language, SettingId.FrameRate, SettingId.NumberFormat, SettingId.CompanionStrip })
                Assert.That(SettingsScreen.OptionCount(id), Is.InRange(2, 4), id.ToString());
        }

        [Test]
        public void SlidersSnapToFivePercent()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.KindOf(SettingId.MusicVolume), Is.EqualTo(SettingKind.Slider));
            Assert.That(SettingsScreen.SetFraction(SettingId.MusicVolume, settings, 0.33), Is.True);
            Assert.That(settings.MusicVolume, Is.EqualTo(0.35).Within(1e-9));
            Assert.That(SettingsScreen.Fraction(SettingId.MusicVolume, settings), Is.EqualTo(0.35).Within(1e-9));
            Assert.That(SettingsScreen.Value(SettingId.MusicVolume, settings), Is.EqualTo("35%"));
            Assert.That(SettingsScreen.SetFraction(SettingId.MusicVolume, settings, 0.34), Is.False, "the same step");
            Assert.That(SettingsScreen.SetFraction(SettingId.MusicVolume, settings, 7), Is.True);
            Assert.That(settings.MusicVolume, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(SettingsScreen.SetFraction(SettingId.AlwaysOnTop, settings, 0.5), Is.False);
        }

        [Test]
        public void TheNumberFormatShowsALiveSampleAndTheStrikeShowsItsOpacity()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            var letters = SettingsScreen.HintLine(SettingId.NumberFormat, settings);
            Assert.That(letters, Does.StartWith("Пример: "));
            SettingsScreen.Select(SettingId.NumberFormat, settings, 3);
            Assert.That(SettingsScreen.HintLine(SettingId.NumberFormat, settings), Is.Not.EqualTo(letters));
            Assert.That(SettingsScreen.HintLine(SettingId.Decimals, settings), Is.EqualTo(SettingsScreen.HintLine(SettingId.NumberFormat, settings)));
            Assert.That(SettingsScreen.HintLine(SettingId.Language, settings), Is.EqualTo(SettingsScreen.Hint(SettingId.Language)));
            Assert.That(SettingsScreen.PreviewAlpha(SettingId.AllyStrikeOpacity, settings), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(SettingsScreen.PreviewAlpha(SettingId.MasterVolume, settings), Is.LessThan(0));
        }

        [Test]
        public void ChangedRowsAreMarkedAndTheTabResetsToDefaults()
        {
            var settings = new GameSettings();
            Assert.That(SettingsScreen.AnyChanged(0, settings), Is.False);

            SettingsScreen.Select(SettingId.Language, settings, 1);
            SettingsScreen.Change(SettingId.Decimals, settings, 1);
            SettingsScreen.SetFraction(SettingId.AllyStrikeOpacity, settings, 0.9);
            SettingsScreen.Change(SettingId.AlwaysOnTop, settings, 1);

            Assert.That(SettingsScreen.IsDefault(SettingId.Language, settings), Is.False);
            Assert.That(SettingsScreen.IsDefault(SettingId.NumberFormat, settings), Is.True);
            Assert.That(SettingsScreen.IsDefault(SettingId.WhatsNew, settings), Is.True);
            Assert.That(SettingsScreen.AnyChanged(0, settings), Is.True);
            var changed = new System.Collections.Generic.List<SettingId>();
            SettingsScreen.ResetTab(0, settings, changed);

            Assert.That(changed, Is.EquivalentTo(new[] { SettingId.Language, SettingId.Decimals, SettingId.AllyStrikeOpacity }));
            Assert.That(settings.Language, Is.EqualTo(new GameSettings().Language));
            Assert.That(settings.NumberFormat.Decimals, Is.EqualTo(new GameSettings().NumberFormat.Decimals));
            Assert.That(settings.AllyStrikeOpacity, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(settings.AlwaysOnTop, Is.False, "another tab is left alone");
            Assert.That(SettingsScreen.AnyChanged(0, settings), Is.False);
            Assert.That(SettingsScreen.CanReset(SettingsScreen.AboutTab), Is.False);
            Assert.That(SettingsScreen.CanReset(SettingsScreen.LookTab), Is.False);
        }

        [Test]
        public void TheCouponRowOpensTheCodeEntry()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();

            Assert.That(SettingsScreen.Rows(SettingsScreen.AboutTab), Does.Contain(SettingId.Coupon));
            Assert.That(SettingsScreen.KindOf(SettingId.Coupon), Is.EqualTo(SettingKind.Action));
            Assert.That(SettingsScreen.Value(SettingId.Coupon, settings), Is.EqualTo("Вставить"));
            Assert.That(SettingsScreen.Change(SettingId.Coupon, settings, 1), Is.True);
        }

        [TearDown]
        public void RestoreLanguage() => Texts.UseLanguage("ru");
    
        [Test]
        public void TheAboutTabHasAnAccountWithEmailPasswordAndTwoCloudIconButtons()
        {
            Texts.UseLanguage("ru");
            var settings = new GameSettings();
            var rows = SettingsScreen.Rows(SettingsScreen.AboutTab);
            foreach (var id in new[] { SettingId.AccountEmail, SettingId.AccountPassword, SettingId.AccountSignIn, SettingId.CloudSave, SettingId.CloudLoad })
            {
                Assert.That(rows, Does.Contain(id));
                Assert.That(SettingsScreen.KindOf(id), Is.EqualTo(SettingKind.Action));
                Assert.That(SettingsScreen.Label(id), Does.Not.StartWith("settings."), id + " has a label");
            }
            Assert.That(SettingsScreen.ActionIcon(SettingId.CloudSave), Is.EqualTo("cloudSave"));
            Assert.That(SettingsScreen.ActionIcon(SettingId.CloudLoad), Is.EqualTo("cloudLoad"));
            Assert.That(SettingsScreen.ActionIcon(SettingId.AccountSignIn), Is.Empty, "sign in is a word, the cloud buttons are icons");
            SettingsScreen.Change(SettingId.AccountPassword, settings, 1);
            Assert.That(SettingsScreen.Account.Focus, Is.EqualTo(AccountField.Password));
            SettingsScreen.Account.Paste("secret123");
            Assert.That(SettingsScreen.HintLine(SettingId.AccountPassword, settings), Does.Not.Contain("secret"), "the password is never shown");
            SettingsScreen.Account.ForgetPassword();
            SettingsScreen.Account.FocusOn(AccountField.None);
        }
}
}
