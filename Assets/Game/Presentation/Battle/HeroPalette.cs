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

        private static bool Coloured(Color32 pixel, out float hue)
        {
            Color.RGBToHSV(pixel, out hue, out var saturation, out var value);
            return saturation >= GreyBelowSaturation && value >= BlackBelowValue;
        }
    }
}
