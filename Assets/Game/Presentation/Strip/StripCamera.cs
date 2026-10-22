using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripCamera
    {
        public static void Fit(Camera camera, int windowWidthPx, int windowHeightPx, int stripHeightPx, int stripOffsetXPx, int stripOffsetFromBottomPx)
        {
            if (windowWidthPx <= 0 || windowHeightPx <= 0 || stripHeightPx <= 0)
                return;
            var pixelsPerUnit = stripHeightPx / StripSpace.Height;
            var halfHeight = windowHeightPx / (2f * pixelsPerUnit);
            var halfWidth = windowWidthPx / (2f * pixelsPerUnit);
            camera.orthographicSize = halfHeight;
            camera.transform.position = new Vector3(
                halfWidth - stripOffsetXPx / pixelsPerUnit,
                halfHeight - stripOffsetFromBottomPx / pixelsPerUnit,
                camera.transform.position.z);
        }

        public static void FitStripOnly(Camera camera)
        {
            camera.orthographicSize = StripSpace.Height / 2f;
            camera.transform.position = new Vector3(StripSpace.Width / 2f, StripSpace.Height / 2f, camera.transform.position.z);
        }
    }
}
