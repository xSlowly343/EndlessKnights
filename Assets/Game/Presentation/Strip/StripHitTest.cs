using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripHitTest
    {
        public static bool IsSpriteAt(Camera camera, int windowLeft, int windowTop, int windowWidth, int windowHeight, int cursorX, int cursorY)
        {
            if (windowWidth <= 0 || windowHeight <= 0)
                return false;

            var clientX = cursorX - windowLeft;
            var clientY = cursorY - windowTop;
            if (clientX < 0 || clientY < 0 || clientX >= windowWidth || clientY >= windowHeight)
                return false;

            var screenPoint = new Vector3(
                clientX * (float)camera.pixelWidth / windowWidth,
                (windowHeight - clientY) * (float)camera.pixelHeight / windowHeight,
                0f);
            Vector2 worldPoint = camera.ScreenToWorldPoint(screenPoint);
            return Physics2D.OverlapPoint(worldPoint) != null;
        }
    }
}
