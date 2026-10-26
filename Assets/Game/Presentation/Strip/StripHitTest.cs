using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripHitTest
    {
        private const int MaxHits = 8;

        private static readonly Collider2D[] Hits = new Collider2D[MaxHits];
        private static readonly ContactFilter2D AnyCollider = new ContactFilter2D().NoFilter();

        public static bool TryGetWorldPoint(Camera camera, int windowLeft, int windowTop, int windowWidth, int windowHeight, int cursorX, int cursorY, out Vector2 worldPoint)
        {
            worldPoint = default;
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
            worldPoint = camera.ScreenToWorldPoint(screenPoint);
            return true;
        }

        public static bool IsInStrip(Vector2 worldPoint) =>
            worldPoint.x >= 0f && worldPoint.x <= StripSpace.Width && worldPoint.y >= 0f && worldPoint.y <= StripSpace.Height;

        public static bool IsSpriteAt(Vector2 worldPoint) => Physics2D.OverlapPoint(worldPoint) != null;

        public static bool IsClickableAt(Vector2 worldPoint)
        {
            var count = Physics2D.OverlapPoint(worldPoint, AnyCollider, Hits);
            for (var i = 0; i < count; i++)
            {
                if (Hits[i].GetComponent<StripClickable>() != null)
                    return true;
            }
            return false;
        }

        public static bool IsClickableAt(Camera camera, int windowLeft, int windowTop, int windowWidth, int windowHeight, int cursorX, int cursorY) =>
            TryGetWorldPoint(camera, windowLeft, windowTop, windowWidth, windowHeight, cursorX, cursorY, out var worldPoint)
            && IsClickableAt(worldPoint);

        public static bool IsSpriteAt(Camera camera, int windowLeft, int windowTop, int windowWidth, int windowHeight, int cursorX, int cursorY) =>
            TryGetWorldPoint(camera, windowLeft, windowTop, windowWidth, windowHeight, cursorX, cursorY, out var worldPoint)
            && IsSpriteAt(worldPoint);
    }
}
