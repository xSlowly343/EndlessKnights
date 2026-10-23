using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripSpace
    {
        public const float BaseWidthPx = 480f;
        public const float BaseHeightPx = 180f;
        public const float PixelsPerUnit = 14f;
        public const float Width = BaseWidthPx / PixelsPerUnit;
        public const float Height = BaseHeightPx / PixelsPerUnit;

        public static Vector2 Centre => new Vector2(Width / 2f, Height / 2f);
        public static Vector2 TopRight(float margin) => new Vector2(Width - margin, Height - margin);
        public static Vector2 BottomLeft(float margin) => new Vector2(margin, margin);
        public static Vector2 BottomRight(float margin) => new Vector2(Width - margin, margin);
        public static float Units(float px) => px / PixelsPerUnit;
        public static Vector2 AtPx(float xPx, float yFromTopPx) => new Vector2(xPx / PixelsPerUnit, Height - yFromTopPx / PixelsPerUnit);
    }
}
