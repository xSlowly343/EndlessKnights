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
        private const float InputOverlayMargin = 0.4f;

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
            PlaceholderSquare.Create(new Color(1f, 0.55f, 0.1f), PlaceholderSize);

            var window = Application.isEditor ? null : WindowController.FindOwnWindow();
            window?.MakeBorderless(StripWidth, StripHeight);
            window?.MakeBackgroundTransparent();
            window?.ShowInTaskbar();
            window?.KeepOnTop();

            var focusButton = FocusModeButton.Create(TopRightCorner(camera, FocusButtonMargin), FocusButtonSize);
            var framePacing = FramePacingDriver.Attach(focusButton.gameObject, TrayMenuState.DefaultFrameRate);
            FocusModeDriver.Attach(focusButton, window);
            if (window == null)
                return;
            var host = focusButton.gameObject;
            var pointer = PointerInput.Attach(host);
            var dock = TaskbarDockDriver.Attach(host, window);
            StripPointerRouter.Attach(host, pointer, window, camera, focusButton, dock);
            var rawInput = RawInputService.Start();
            InputDebugDriver.Attach(focusButton.gameObject, rawInput, InputDebugOverlay.Create(BottomLeftCorner(camera, InputOverlayMargin)));
            var clickThrough = ClickThroughDriver.Attach(host, window, camera, pointer);
            FullscreenDriver.Attach(focusButton.gameObject, window);

            var tray = TrayIcon.Create(Application.productName);
            if (tray != null)
                TrayDriver.Attach(focusButton.gameObject, tray, window, clickThrough, framePacing, dock);
        }

        private static Vector2 BottomLeftCorner(Camera camera, float margin)
        {
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * StripWidth / StripHeight;
            var centre = (Vector2)camera.transform.position;
            return centre + new Vector2(-halfWidth + margin, -halfHeight + margin);
        }

        private static Vector2 TopRightCorner(Camera camera, float margin)
        {
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * StripWidth / StripHeight;
            var centre = (Vector2)camera.transform.position;
            return centre + new Vector2(halfWidth - margin, halfHeight - margin);
        }
    }
}
