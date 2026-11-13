using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Platform.Windows.Tray;
using Game.Presentation.Battle;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class SettingsDriver : MonoBehaviour
    {
        private SettingsStore _store = null!;
        private TrayDriver? _tray;
        private TaskbarDockDriver? _dock;
        private PanelModeDriver? _panelMode;
        private BattleView? _battle;
        private GameLoop? _loop;
        private NotificationDriver? _notifications;
        private ChangeLog? _changeLog;
        private bool _firstRun;
        private PanelCanvas? _panels;
        private FullscreenDriver? _fullscreen;

        public GameSettings Settings { get; private set; } = new GameSettings();

        public static SettingsDriver Attach(GameObject host)
        {
            var driver = host.AddComponent<SettingsDriver>();
            driver._store = new SettingsStore(SaveDriver.SaveDirectory);
            var problems = new List<string>();
            driver._firstRun = !driver._store.Exists;
            driver.Settings = driver._store.Load(problems);
            driver.Settings.Paused = false;
            if (driver._firstRun)
                PrivacyPolicy.Accept(driver.Settings);
            foreach (var problem in problems)
                Debug.LogWarning("Settings: " + problem);
            ApplyText(driver.Settings);
            return driver;
        }

        public void Bind(PanelCanvas panels, PanelModeDriver? panelMode, TaskbarDockDriver? dock, TrayDriver? tray, BattleView? battle, GameLoop? loop, NotificationDriver? notifications, ChangeLog? changeLog, FullscreenDriver? fullscreen = null)
        {
            _changeLog = changeLog;
            _panels = panels;
            _loop = loop;
            _notifications = notifications;
            ApplyPermissions();
            _panelMode = panelMode;
            _dock = dock;
            _tray = tray;
            _battle = battle;
            ApplySound();
            _fullscreen = fullscreen;
            ApplyStrikeOpacity();
            HeroModel.UseCosmetics(loop?.Cosmetics);
            SettingsScreen.UseLooks(loop == null ? null : new HeroLookChooser(loop.Cosmetics));
            ApplyLook();
            ApplyText(Settings);
            panels.Settings.Use(Settings);
            panels.Settings.UseVersion(Application.version);
            panels.Settings.Changed += OnChanged;
            panels.ConsentAccepted += () => AnswerConsent(true);
            panels.ConsentDeclined += () => AnswerConsent(false);
            if (_dock != null)
            {
                _dock.UseOffset((int)Settings.StripOffset);
                _dock.Moved += OnMoved;
            }
            if (_tray != null)
            {
                _tray.UseState(TrayStateOf(Settings));
                _tray.StateChanged += OnTrayChanged;
            }
            _fullscreen?.UseAutoHide(Settings.AutoHideFullscreen);
            ApplyHideHotKey(false);
            var showWhatsNew = WhatsNew.ShowOnStart(Settings, _changeLog, _firstRun);
            Save();
            if (showWhatsNew)
                ShowWhatsNew();
            else
                _panelMode?.Refresh();
        }

        private void AnswerConsent(bool given)
        {
            if (given)
                PrivacyPolicy.Accept(Settings);
            else
                Settings.InputConsentGiven = false;
            ApplyPermissions();
            Save();
            _panelMode?.FinishConsent();
            if (!given)
                _notifications?.Notify(ConsentTexts.Declined);
        }

        private void OnChanged(SettingId id)
        {
            switch (id)
            {
                case SettingId.Language:
                case SettingId.NumberFormat:
                case SettingId.Decimals:
                    ApplyText(Settings);
                    _battle?.Relabel();
                    _panelMode?.Refresh();
                    break;
                case SettingId.CountKeys:
                case SettingId.CountMouseMovement:
                case SettingId.CountClicks:
                case SettingId.CountWheel:
                    ApplyPermissions();
                    break;
                case SettingId.HeroStrikeOpacity:
                case SettingId.AllyStrikeOpacity:
                    ApplyStrikeOpacity();
                    break;
                case SettingId.MasterVolume:
                case SettingId.InterfaceVolume:
                    ApplySound();
                    break;
                case SettingId.HeroCloak:
                case SettingId.HeroEyes:
                case SettingId.HeroStrike:
                    ApplyLook();
                    return;
                case SettingId.InputConsent:
                    _panelMode?.OpenConsent();
                    return;
                case SettingId.WhatsNew:
                    ShowWhatsNew();
                    return;
                case SettingId.Coupon:
                    if (_loop == null)
                        _panels?.ShowNotice(CouponText.Offline);
                    return;
                case SettingId.BuildCopy:
                    CopyBuild();
                    return;
                case SettingId.BuildPaste:
                    PasteBuild();
                    return;
                case SettingId.ResetPosition:
                    _dock?.UseOffset((int)Settings.StripOffset);
                    break;
                case SettingId.AutoHideFullscreen:
                    _fullscreen?.UseAutoHide(Settings.AutoHideFullscreen);
                    break;
                case SettingId.HideHotKey:
                    ApplyHideHotKey(true);
                    break;
                case SettingId.Paused:
                case SettingId.AlwaysOnTop:
                case SettingId.ClickThrough:
                case SettingId.FrameRate:
                case SettingId.ShowInputDebug:
                    _tray?.UseState(TrayStateOf(Settings));
                    break;
            }
            Save();
        }

        private void CopyBuild()
        {
            var code = _loop?.ShareBuild() ?? string.Empty;
            if (code.Length > 0)
                GUIUtility.systemCopyBuffer = code;
            _panels?.ShowNotice(code.Length > 0 ? BuildCodeText.Copied : BuildCodeText.NothingToCopy);
        }

        private void PasteBuild()
        {
            if (_loop == null)
                return;
            var code = GUIUtility.systemCopyBuffer;
            var build = _loop.ReadBuild(code);
            var result = _loop.ImportBuild(code);
            var tree = _loop.ClassTreeDef;
            _panels?.ShowNotice(result == BuildImportResult.Done && build != null && tree != null ? BuildCodeText.Imported(build, tree, _loop.Data.Gems) : BuildCodeText.Result(result));
        }

        private void ShowWhatsNew()
        {
            var latest = _changeLog?.Latest;
            if (latest != null)
                _panelMode?.OpenReport(SettingsScreen.WhatsNewTitle(latest.Game), latest.Lines(Texts.Language), Texts.Get("report.gotIt"));
        }

        private void OnTrayChanged(TrayMenuState state)
        {
            Settings.Paused = false;
            Settings.ClickThrough = state.ClickThrough;
            Settings.AlwaysOnTop = state.AlwaysOnTop;
            Settings.FrameRate = state.FrameRate;
            Settings.ShowInputDebug = state.ShowInputDebug;
            Save();
        }

        private void OnMoved(int offset)
        {
            Settings.StripOffset = offset;
            Save();
        }

        private void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(_store.Directory);
                _store.Write(Settings);
            }
            catch (System.Exception error) when (error is System.IO.IOException || error is System.UnauthorizedAccessException)
            {
                Debug.LogWarning("Settings: not saved, " + error.Message);
            }
        }

        private void ApplyPermissions()
        {
            if (_loop != null)
            {
                _loop.Permissions = InputPermissions.From(Settings);
                _loop.Strip.Mode = SettingsScreen.StripModeOf(Settings);
            }
        }

        private void ApplyHideHotKey(bool tellWhenBusy)
        {
            if (_tray == null)
                return;
            var registered = _tray.UseHideHotKey(Settings.HideHotKey);
            if (!registered && tellWhenBusy)
                _panels?.ShowNotice(Texts.Get("settings.hideHotKeyBusy"));
        }

        private void ApplySound() => _battle?.UseSoundVolume(TransitionSound.Volume(Settings));

        private void ApplyStrikeOpacity() => _battle?.UseStrikeOpacity((float)Settings.HeroStrikeOpacity, (float)Settings.AllyStrikeOpacity);

        private void ApplyLook()
        {
            if (_battle != null && SettingsScreen.Looks != null)
                _battle.UseLook(SettingsScreen.Looks.Shown);
        }

        private static void ApplyText(GameSettings settings)
        {
            Texts.UseLanguage(settings.Language);
            NumberDisplay.Use(settings.NumberFormat);
        }

        private static TrayMenuState TrayStateOf(GameSettings settings) =>
            new TrayMenuState(false, settings.ClickThrough, settings.AlwaysOnTop, settings.FrameRate, settings.ShowInputDebug);
    }
}
