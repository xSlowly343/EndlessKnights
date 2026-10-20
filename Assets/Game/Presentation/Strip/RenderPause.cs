using UnityEngine.Rendering;

namespace Game.Presentation.Strip
{
    public static class RenderPause
    {
        private const int PausedFrameInterval = 60;

        public static void Set(bool paused)
        {
            OnDemandRendering.renderFrameInterval = paused ? PausedFrameInterval : 1;
        }
    }
}
