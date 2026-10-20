using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class FrameRateLimit
    {
        public static void Apply(int framesPerSecond)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = framesPerSecond;
        }
    }
}
