using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class FrameRateLimit
    {
        public static void DisableEngineLimit()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }
    }
}
