using System.Collections.Generic;
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

        public GameSettings Settings { get; private set; } = new GameSettings();

        public static SettingsDriver Attach(GameObject host)
        {
            var driver = host.AddComponent<SettingsDriver>();
            driver._store = new SettingsStore(SaveDriver.SaveDirectory);
            var problems = new List<string>();
            driver.Settings = driver._store.Load(problems);
            foreach (var problem in problems)
                Debug.LogWarning("Settings: " + problem);
            ApplyText(driver.Settings);
            return driver;
        }

        public void Bind(PanelCanvas panels, PanelModeDriver? panelMode, TaskbarDockDriver? dock, TrayDriver? tray, BattleView? battle, GameLoop? loop)
        {
            _loop = loop;
            ApplyPermissions();
            _panelMode = panelMode;
            _dock = dock;
            _tray = tray;
            _battle = battle;
            ApplyText(Settings);
            panels.Settings.Use(Settings);
            panels.Settings.Changed += OnChanged;
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
            _panelMode?.Refresh();
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
                case SettingId.ResetPosition:
                    _dock?.UseOffset((int)Settings.StripOffset);
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

        private void OnTrayChanged(TrayMenuState state)
        {
            Settings.Paused = state.Paused;
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
                _loop.Permissions = new InputPermissions(true, Settings.CountKeys, Settings.CountMouseMovement, Settings.CountClicks, Settings.CountWheel);
        }

        private static void ApplyText(GameSettings settings)
        {
            Texts.UseLanguage(settings.Language);
            NumberDisplay.Use(settings.NumberFormat);
        }

        private static TrayMenuState TrayStateOf(GameSettings settings) =>
            new TrayMenuState(settings.Paused, settings.ClickThrough, settings.AlwaysOnTop, settings.FrameRate, settings.ShowInputDebug);
    }
}
