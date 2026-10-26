using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripIcons
    {
        private const int Size = 64;
        private const float BarHalfWidth = 18f;
        private const float BarHalfHeight = 3.5f;
        private const float BarStep = 12f;
        private const float RingRadius = 17f;
        private const float RingHalfWidth = 3.5f;
        private const float DotRadius = 6f;
        private const float SlashHalfWidth = 3.5f;

        private static Sprite? _menu;
        private static Sprite? _focus;
        private static Sprite? _noFocus;

        public static Sprite Menu => _menu ??= Create(MenuGlyph);
        public static Sprite Focus => _focus ??= Create(TargetGlyph);
        public static Sprite NoFocus => _noFocus ??= Create(CrossedTargetGlyph);

        private delegate float GlyphShape(float x, float y);

        private static Sprite Create(GlyphShape glyph)
        {
            var texture = new Texture2D(Size, Size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var half = Size / 2f;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(glyph(px, py))));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        private static float RoundedRect(float x, float y, float halfWidth, float halfHeight, float corner)
        {
            var qx = Mathf.Abs(x) - halfWidth + corner;
            var qy = Mathf.Abs(y) - halfHeight + corner;
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
        }

        private static float Bar(float x, float y) => RoundedRect(x, y, BarHalfWidth, BarHalfHeight, BarHalfHeight);

        private static float MenuGlyph(float x, float y)
        {
            var top = Bar(x, y - BarStep);
            var middle = Bar(x, y);
            var bottom = Bar(x, y + BarStep);
            return Mathf.Min(top, Mathf.Min(middle, bottom));
        }

        private static float TargetGlyph(float x, float y)
        {
            var distance = Mathf.Sqrt(x * x + y * y);
            var ring = Mathf.Abs(distance - RingRadius) - RingHalfWidth;
            var dot = distance - DotRadius;
            return Mathf.Min(ring, dot);
        }

        private static float CrossedTargetGlyph(float x, float y)
        {
            var slash = Mathf.Abs(x - y) / Mathf.Sqrt(2f) - SlashHalfWidth;
            var limited = Mathf.Max(slash, Mathf.Sqrt(x * x + y * y) - (RingRadius + RingHalfWidth));
            return Mathf.Min(TargetGlyph(x, y), limited);
        }
    }
}
