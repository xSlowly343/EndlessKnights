using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    public static class GameStartup
    {
        public const int StripWidth = 480;
        public const int StripHeight = 140;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (Application.isEditor)
                return;

            WindowController.FindOwnWindow()?.MakeBorderless(StripWidth, StripHeight);
        }
    }
}
