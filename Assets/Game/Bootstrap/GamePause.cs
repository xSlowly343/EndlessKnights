using UnityEngine;

namespace Game.Bootstrap
{
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static void Set(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }
    }
}
