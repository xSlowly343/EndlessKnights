using System;
using UnityEngine.Rendering;

namespace Game.Presentation.Strip
{
    [Flags]
    public enum RenderPauseReason
    {
        None = 0,
        Fullscreen = 1,
        SessionLocked = 2
    }

    public static class RenderPause
    {
        private const int PausedFrameInterval = 60;

        private static RenderPauseReason _reasons;

        public static void Set(RenderPauseReason reason, bool active)
        {
            _reasons = active ? _reasons | reason : _reasons & ~reason;
            OnDemandRendering.renderFrameInterval = _reasons == RenderPauseReason.None ? 1 : PausedFrameInterval;
        }
    }
}
