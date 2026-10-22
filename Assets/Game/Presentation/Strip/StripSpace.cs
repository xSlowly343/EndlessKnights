using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripSpace
    {
        public const float Height = 10f;
        public const float BaseWidthPx = 480f;
        public const float BaseHeightPx = 140f;
        public const float Width = Height * BaseWidthPx / BaseHeightPx;

        public static Vector2 Centre => new Vector2(Width / 2f, Height / 2f);
        public static Vector2 TopRight(float margin) => new Vector2(Width - margin, Height - margin);
        public static Vector2 BottomLeft(float margin) => new Vector2(margin, margin);
    }
}
