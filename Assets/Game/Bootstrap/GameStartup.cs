using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public static class GameStartup
    {
        public const int StripWidth = 480;
        public const int StripHeight = 140;

        private const float PlaceholderSize = 4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            var camera = Camera.main;
            if (camera != null)
                TransparentCamera.Apply(camera);
            PlaceholderSquare.Create(new Color(1f, 0.55f, 0.1f), PlaceholderSize);

            if (Application.isEditor)
                return;

            var window = WindowController.FindOwnWindow();
            window?.MakeBorderless(StripWidth, StripHeight);
            window?.MakeBackgroundTransparent();
        }
    }
}
