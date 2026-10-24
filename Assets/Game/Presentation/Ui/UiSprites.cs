using UnityEngine;

namespace Game.Presentation.Ui
{
    public static class UiSprites
    {
        public const float CornerPx = 12f;

        private const int RoundedSize = 32;
        private const int CircleSize = 64;
        private const float PixelsPerUnit = 100f;

        private static Sprite? _rounded;
        private static Sprite? _circle;

        public static Sprite Rounded => _rounded ??= CreateRounded();
        public static Sprite Circle => _circle ??= CreateCircle();

        public static float MultiplierFor(float radiusPx) => CornerPx / radiusPx;

        private static Sprite CreateRounded()
        {
            var texture = NewTexture(RoundedSize);
            var low = CornerPx;
            var high = RoundedSize - CornerPx;
            for (var y = 0; y < RoundedSize; y++)
            {
                for (var x = 0; x < RoundedSize; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var dx = px < low ? low - px : px > high ? px - high : 0f;
                    var dy = py < low ? low - py : py > high ? py - high : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(CornerPx, dx, dy)));
                }
            }
            texture.Apply();
            var border = new Vector4(CornerPx, CornerPx, CornerPx, CornerPx);
            return Sprite.Create(texture, new Rect(0f, 0f, RoundedSize, RoundedSize), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateCircle()
        {
            var texture = NewTexture(CircleSize);
            var radius = CircleSize / 2f;
            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(radius, x + 0.5f - radius, y + 0.5f - radius)));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, CircleSize, CircleSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        private static float Coverage(float radius, float dx, float dy) => Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    }
}
