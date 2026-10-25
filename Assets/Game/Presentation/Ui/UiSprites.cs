using UnityEngine;

namespace Game.Presentation.Ui
{
    public static class UiSprites
    {
        public const float CornerPx = 12f;

        private const int RoundedSize = 32;
        private const int CircleSize = 64;
        private const float PixelsPerUnit = 100f;

        private const float RingInnerFraction = 0.62f;
        private const float RaisedTopShade = 1f;
        private const float RaisedBottomShade = 0.84f;
        private const float RaisedDepthShade = 0.6f;
        private const float RaisedDepthPx = 3f;
        private const int DiamondSize = 32;

        private static Sprite? _rounded;
        private static Sprite? _circle;
        private static Sprite? _ring;
        private static Sprite? _rainbow;
        private static Sprite? _raised;
        private static Sprite? _diamond;
        private static Texture2D? _nodeAtlas;

        public static readonly Rect DiscUv = new Rect(0f, 0f, 0.5f, 1f);
        public static readonly Rect RingUv = new Rect(0.5f, 0f, 0.5f, 1f);

        private static readonly Color[] RainbowStops =
        {
            new Color32(0xFF, 0x5A, 0x5A, 0xFF),
            new Color32(0xF0, 0xB4, 0x3C, 0xFF),
            new Color32(0x4F, 0xC2, 0x77, 0xFF),
            new Color32(0x5B, 0x9B, 0xFF, 0xFF),
            new Color32(0xB0, 0x7C, 0xFF, 0xFF),
        };

        public static Sprite Rounded => _rounded ??= CreateRounded(_ => Color.white);
        public static Sprite RainbowRounded => _rainbow ??= CreateRounded(Rainbow);
        public static Sprite RaisedRounded => _raised ??= CreateRounded(Raised, RaisedDepthPx);
        public static Sprite Diamond => _diamond ??= CreateDiamond();
        public static Sprite Circle => _circle ??= CreateCircle(0f);
        public static Sprite Ring => _ring ??= CreateCircle(RingInnerFraction);

        public static Texture2D NodeAtlas => _nodeAtlas ??= CreateNodeAtlas();

        public static float MultiplierFor(float radiusPx) => CornerPx / radiusPx;

        private static Sprite CreateRounded(System.Func<float, Color> tint) => CreateRounded((t, _) => tint(t), 0f);

        private static Sprite CreateRounded(System.Func<float, float, Color> tint, float depthPx)
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
                    var color = tint((px + RoundedSize - py) / (2f * RoundedSize), py < depthPx ? 0f : py / RoundedSize);
                    color.a = Coverage(CornerPx, dx, dy);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            var border = new Vector4(CornerPx, CornerPx, CornerPx, CornerPx);
            return Sprite.Create(texture, new Rect(0f, 0f, RoundedSize, RoundedSize), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateCircle(float innerFraction)
        {
            var texture = NewTexture(CircleSize);
            var radius = CircleSize / 2f;
            var inner = radius * innerFraction;
            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                {
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    var hole = inner > 0f ? Coverage(inner, dx, dy) : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(radius, dx, dy) - hole));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, CircleSize, CircleSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Sprite CreateDiamond()
        {
            var texture = NewTexture(DiamondSize);
            var half = DiamondSize / 2f;
            for (var y = 0; y < DiamondSize; y++)
            {
                for (var x = 0; x < DiamondSize; x++)
                {
                    var distance = Mathf.Abs(x + 0.5f - half) + Mathf.Abs(y + 0.5f - half);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(half - distance)));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, DiamondSize, DiamondSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Color Raised(float diagonal, float height)
        {
            var shade = height <= 0f ? RaisedDepthShade : Mathf.Lerp(RaisedBottomShade, RaisedTopShade, height);
            return new Color(shade, shade, shade, 1f);
        }

        private static Texture2D CreateNodeAtlas()
        {
            var texture = new Texture2D(2 * CircleSize, CircleSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var radius = CircleSize / 2f;
            var inner = radius * RingInnerFraction;
            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                {
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    var disc = Coverage(radius - 1f, dx, dy);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, disc));
                    texture.SetPixel(CircleSize + x, y, new Color(1f, 1f, 1f, disc - Coverage(inner, dx, dy)));
                }
            }
            texture.Apply();
            return texture;
        }

        private static Color Rainbow(float t)
        {
            var scaled = Mathf.Clamp01(t) * (RainbowStops.Length - 1);
            var index = Mathf.Min((int)scaled, RainbowStops.Length - 2);
            return Color.Lerp(RainbowStops[index], RainbowStops[index + 1], scaled - index);
        }

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        private static float Coverage(float radius, float dx, float dy) => Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    }
}
