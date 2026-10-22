using Game.Platform.Windows.Input;
using Game.Platform.Windows.Instance;
using Game.Platform.Windows.Tray;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    public static class GameStartup
    {
        public const int StripWidth = 480;
        public const int StripHeight = 140;

        private const float PlaceholderSize = 4f;
        private const float FocusButtonSize = 1.6f;
        private const float FocusButtonMargin = 1.2f;
        private const float InputOverlayMargin = 0.3f;
        private const float MenuButtonGap = 0.4f;
        private static readonly Color MenuButtonColor = new Color(0.8f, 0.2f, 0.2f);

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
            PlaceholderSquare.Create(new Color(1f, 0.55f, 0.1f), PlaceholderSize).transform.position = StripSpace.Centre;

            var window = Application.isEditor ? null : WindowController.FindOwnWindow();
            window?.MakeBorderless(StripWidth, StripHeight);
            window?.MakeBackgroundTransparent();
            window?.ShowInTaskbar();
            window?.KeepOnTop();

            var focusButton = FocusModeButton.Create(StripSpace.TopRight(FocusButtonMargin), FocusButtonSize);
            var framePacing = FramePacingDriver.Attach(focusButton.gameObject, TrayMenuState.DefaultFrameRate);
            FocusModeDriver.Attach(focusButton, window);
            if (window == null)
            {
                var editorData = GameDataFiles.TryLoadAll();
                if (editorData != null)
                    GameDataHotReload.Attach(focusButton.gameObject, editorData);
                var editorInput = UnityInputSource.Attach(focusButton.gameObject);
                InputDebugDriver.Attach(focusButton.gameObject, editorInput, InputDebugOverlay.Create(StripSpace.BottomLeft(InputOverlayMargin)));
                return;
            }
            var host = focusButton.gameObject;
            var pointer = PointerInput.Attach(host);
            var dock = TaskbarDockDriver.Attach(host, window);
            StripCameraDriver.Attach(host, camera, dock);
            var menuButton = StripButton.Create("MenuButton", MenuButtonColor, StripSpace.TopRight(FocusButtonMargin) - new Vector2(FocusButtonSize + MenuButtonGap, 0f), new Vector2(FocusButtonSize, FocusButtonSize));
            var panels = PanelView.Create();
            var panelMode = PanelModeDriver.Attach(host, dock, panels, menuButton);
            StripPointerRouter.Attach(host, pointer, window, camera, focusButton, dock, menuButton, panels);
            var rawInput = RawInputService.Start(GameDataFiles.TryLoadInputCaps()?.Detection);
            var inputDebug = InputDebugOverlay.Create(StripSpace.BottomLeft(InputOverlayMargin));
            InputDebugDriver.Attach(focusButton.gameObject, rawInput, inputDebug, rawInput, window);
            var clickThrough = ClickThroughDriver.Attach(host, window, camera, pointer);
            FullscreenDriver.Attach(focusButton.gameObject, window);

            var tray = TrayIcon.Create(Application.productName);
            if (tray != null)
                TrayDriver.Attach(focusButton.gameObject, tray, window, clickThrough, framePacing, dock, rawInput, inputDebug, panelMode);
        }
    }
}
