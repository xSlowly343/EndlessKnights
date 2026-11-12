using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class HeroPalette
    {
        private const float GreyBelowSaturation = 0.12f;
        private const float BlackBelowValue = 0.04f;
        private const float CloakFromHue = 230f / 360f;
        private const float CloakToHue = 330f / 360f;
        private const float GlowFromHue = 80f / 360f;
        private const float GlowToHue = 180f / 360f;
        private const int HighlightAbove = 125;
        private const int StripeRows = 3;
        private const int StarEvery = 11;

        public static readonly Color32 CloakReference = new Color32(81, 51, 115, 255);
        public static readonly Color32 GlowReference = new Color32(111, 253, 146, 255);

        public static bool IsCloak(Color32 pixel) => Coloured(pixel, out var hue) && hue >= CloakFromHue && hue <= CloakToHue;

        public static bool IsGlow(Color32 pixel) => Coloured(pixel, out var hue) && hue >= GlowFromHue && hue <= GlowToHue;

        public static Color32 Shifted(Color32 pixel, Color target, Color32 reference)
        {
            Color.RGBToHSV(pixel, out _, out var saturation, out var value);
            Color.RGBToHSV(target, out var targetHue, out var targetSaturation, out var targetValue);
            Color.RGBToHSV(reference, out _, out var referenceSaturation, out var referenceValue);
            Color32 shifted = Color.HSVToRGB(targetHue, Mathf.Clamp01(saturation * targetSaturation / referenceSaturation), Mathf.Clamp01(value * targetValue / referenceValue));
            shifted.a = pixel.a;
            return shifted;
        }

        public static void Recolor(Color32[] pixels, Color? cloak, Color? glow)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a == 0)
                    continue;
                if (cloak.HasValue && IsCloak(pixel))
                    pixels[i] = Shifted(pixel, cloak.Value, CloakReference);
                else if (glow.HasValue && IsGlow(pixel))
                    pixels[i] = Shifted(pixel, glow.Value, GlowReference);
            }
        }

        public static void Recolor(Color32[] pixels, int width, int cellWidth, CloakPaint cloak, Color? glow)
        {
            var mask = new bool[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a == 0)
                    continue;
                if (IsCloak(pixel))
                    mask[i] = true;
                else if (glow.HasValue && IsGlow(pixel))
                    pixels[i] = Shifted(pixel, glow.Value, GlowReference);
            }
            var height = pixels.Length / width;
            for (var left = 0; left < width; left += cellWidth)
                PaintCell(pixels, mask, width, height, left, Mathf.Min(left + cellWidth, width), cloak);
        }

        private static void PaintCell(Color32[] pixels, bool[] mask, int width, int height, int left, int right, CloakPaint cloak)
        {
            var top = -1;
            var bottom = height;
            for (var y = 0; y < height; y++)
            {
                for (var x = left; x < right; x++)
                {
                    if (!mask[y * width + x])
                        continue;
                    top = Mathf.Max(top, y);
                    bottom = Mathf.Min(bottom, y);
                }
            }
            if (top < 0)
                return;
            var span = Mathf.Max(1, top - bottom);
            for (var y = bottom; y <= top; y++)
            {
                for (var x = left; x < right; x++)
                {
                    var index = y * width + x;
                    if (!mask[index])
                        continue;
                    var pixel = pixels[index];
                    var fromTop = top - y;
                    if (cloak.Pattern == CloakPattern.Gradient)
                        pixels[index] = Shifted(pixel, Color.Lerp(cloak.First, cloak.Second, (float)fromTop / span), CloakReference);
                    else if (!TakesSecond(cloak.Pattern, pixels, width, height, left, right, x, y, fromTop))
                        pixels[index] = Shifted(pixel, cloak.First, CloakReference);
                    else if (cloak.Pattern == CloakPattern.Stars)
                        pixels[index] = Solid(cloak.Second, pixel.a);
                    else
                        pixels[index] = Shifted(pixel, cloak.Second, CloakReference);
                }
            }
        }

        private static bool TakesSecond(CloakPattern pattern, Color32[] pixels, int width, int height, int left, int right, int x, int y, int fromTop)
        {
            switch (pattern)
            {
                case CloakPattern.Trim:
                    return Clear(pixels, width, height, left, right, x - 1, y) || Clear(pixels, width, height, left, right, x + 1, y)
                        || Clear(pixels, width, height, left, right, x, y - 1) || Clear(pixels, width, height, left, right, x, y + 1);
                case CloakPattern.Duotone:
                    var pixel = pixels[y * width + x];
                    return Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > HighlightAbove;
                case CloakPattern.Stripes:
                    return fromTop / StripeRows % 2 == 1;
                case CloakPattern.Stars:
                    return ((((x - left) * 73856093) ^ (fromTop * 19349663)) & int.MaxValue) % StarEvery == 0;
                default:
                    return false;
            }
        }

        private static bool Clear(Color32[] pixels, int width, int height, int left, int right, int x, int y)
        {
            return x < left || x >= right || y < 0 || y >= height || pixels[y * width + x].a == 0;
        }

        private static Color32 Solid(Color color, byte alpha)
        {
            Color32 solid = color;
            solid.a = alpha;
            return solid;
        }

        private static bool Coloured(Color32 pixel, out float hue)
        {
            Color.RGBToHSV(pixel, out hue, out var saturation, out var value);
            return saturation >= GreyBelowSaturation && value >= BlackBelowValue;
        }
    }
}
