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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
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

            var focusButton = FocusModeButton.Create(camera, TopRightCorner(camera, FocusButtonMargin), FocusButtonSize);
            FocusModeDriver.Attach(focusButton, window);
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
