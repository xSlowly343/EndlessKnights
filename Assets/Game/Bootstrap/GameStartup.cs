using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Platform.Windows.Input;
using Game.Platform.Windows.Instance;
using Game.Platform.Windows.Tray;
using Game.Platform.Windows.Window;
using Game.Presentation.Battle;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    public static class GameStartup
    {
        public const int StripWidth = 480;
        public const int StripHeight = 180;

        private const float StripButtonSizePx = 28f;
        private const float InputOverlayMargin = 0.3f;
        private static readonly Vector2 MenuButtonPosition = StripSpace.AtPx(455f, 20f);
        private static readonly Vector2 FocusButtonPosition = StripSpace.AtPx(419f, 20f);

        private static bool _isSecondInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureSingleInstance()
        {
            if (Application.isEditor || SingleInstance.TryClaim())
                return;

            _isSecondInstance = true;
            SingleInstance.ActivateRunningInstance();
            Application.Quit();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (_isSecondInstance)
                return;

            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            var camera = Camera.main;
            if (camera == null)
                return;

            TransparentCamera.Apply(camera);
            StripCamera.FitStripOnly(camera);

            var window = Application.isEditor ? null : WindowController.FindOwnWindow();
            window?.MakeBorderless(StripWidth, StripHeight);
            window?.MakeBackgroundTransparent();
            window?.ShowInTaskbar();
            window?.KeepOnTop();

            var focusButton = FocusModeButton.Create(FocusButtonPosition, StripSpace.Units(StripButtonSizePx));
            var framePacing = FramePacingDriver.Attach(focusButton.gameObject, TrayMenuState.DefaultFrameRate);
            if (window == null)
            {
                var editorData = GameDataFiles.TryLoadAll();
                var editorInput = new ObservedInputSource(UnityInputSource.Attach(focusButton.gameObject));
                InputDebugDriver.Attach(focusButton.gameObject, editorInput, InputDebugOverlay.Create(StripSpace.BottomLeft(InputOverlayMargin)));
                if (editorData == null)
                    return;
                var editorBattle = BattleDriver.Attach(focusButton.gameObject, editorData.Current, editorInput);
                editorData.Reloaded += editorBattle.UseData;
                GameDataHotReload.Attach(focusButton.gameObject, editorData);
                return;
            }
            var host = focusButton.gameObject;
            var pointer = PointerInput.Attach(host);
            var dock = TaskbarDockDriver.Attach(host, window);
            StripCameraDriver.Attach(host, camera, dock);
            var menuButton = StripButton.CreateIcon("MenuButton", StripIcons.Menu, MenuButtonPosition, StripSpace.Units(StripButtonSizePx));
            var panels = PanelCanvas.Create();
            var panelMode = PanelModeDriver.Attach(host, dock, panels, menuButton);
            var router = StripPointerRouter.Attach(host, pointer, window, camera, focusButton, dock, menuButton, panels);
            var data = GameDataFiles.TryLoadAll();
            var settings = SettingsDriver.Attach(host);
            BattleView? battleView = null;
            GameLoop? loop = null;
            var rawInput = RawInputService.Start(data?.Current.InputCaps.Detection);
            var observedInput = new ObservedInputSource(rawInput);
            InputShutdownDriver.Attach(host, rawInput);
            var inputDebug = InputDebugOverlay.Create(StripSpace.BottomLeft(InputOverlayMargin));
            InputDebugDriver.Attach(focusButton.gameObject, observedInput, inputDebug, rawInput, window);
            if (data != null)
            {
                var save = SaveDriver.TryLoad(data.Current);
                var battle = BattleDriver.Attach(host, data.Current, observedInput, save?.State);
                panels.Bind(battle.Loop, data.Current.Tree);
                router.UseSkillBar(battle.SkillBar);
                battleView = battle.View;
                loop = battle.Loop;
                NotificationDriver.Attach(host, dock, panelMode, panels, battle);
                if (save != null)
                    ReportOffline(battle, panelMode, save.AwayTime(Game.Platform.Windows.Power.SystemClock.Read()).Seconds);
                SaveDriver.Attach(host, battle.Loop.State, save?.Clock);
            }
            var clickThrough = ClickThroughDriver.Attach(host, window, camera, pointer, panels, focusButton);
            FocusModeDriver.Attach(host, window, clickThrough);
            FullscreenDriver.Attach(focusButton.gameObject, window);

            var tray = TrayIcon.Create(Application.productName);
            var trayDriver = tray != null ? TrayDriver.Attach(focusButton.gameObject, tray, window, clickThrough, framePacing, dock, rawInput, inputDebug, panelMode) : null;
            settings.Bind(panels, panelMode, dock, trayDriver, battleView, loop);
        }

        private static void ReportOffline(BattleDriver battle, PanelModeDriver panelMode, double awaySeconds)
        {
            var result = battle.ApplyOffline(awaySeconds);
            if (result != null && OfflineReport.ShouldShow(awaySeconds, result))
                panelMode.OpenReport(OfflineReport.Title, OfflineReport.Lines(awaySeconds, result, NumberDisplay.Settings));
        }
    }
}
