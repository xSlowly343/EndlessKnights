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
        private const float PearlPeriod = 14f;
        private const float PearlSheenShift = 0.2f;
        private const float AuroraPeriod = 12f;
        private const float AuroraSway = 0.7f;
        private const float AuroraSwayDepth = 0.12f;
        private const float EmberPeriod = 7f;
        private const int EmberEvery = 3;
        private const int EmberRise = 9;
        private const int TwinkleSteps = 8;
        private const float ShootFrom = 0.62f;
        private const float ShootTo = 0.82f;
        private const float ShootSlope = 0.6f;
        private const int ShootTail = 3;
        private const float BoltSeconds = 0.07f;
        private const float SecondBoltAt = 0.56f;
        private const float BoltSlope = 0.25f;
        private const float FrostGrowUntil = 0.65f;
        private const float FrostReach = 0.6f;
        private const int FrostGlitterEvery = 7;
        private const int FrostGlitterSteps = 64;
        private const float RipplePeriod = 6f;
        private const float RippleSway = 0.9f;
        private const float RippleDepth = 1.4f;
        private const float RippleCrest = 0.86f;
        private const int ThreadStep = 8;
        private const float GlintWidth = 0.12f;
        private const float GlintShare = 0.5f;
        private const float Turn = Mathf.PI * 2f;

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
            Recolor(pixels, width, cellWidth, cloak, glow, 0f);
        }

        public static void Recolor(Color32[] pixels, int width, int cellWidth, CloakPaint cloak, Color? glow, float phase)
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
                PaintCell(pixels, mask, width, height, left, Mathf.Min(left + cellWidth, width), cloak, phase - Mathf.Floor(phase));
        }

        private static void PaintCell(Color32[] pixels, bool[] mask, int width, int height, int left, int right, CloakPaint cloak, float phase)
        {
            var top = -1;
            var bottom = height;
            var first = right;
            var last = left;
            for (var y = 0; y < height; y++)
            {
                for (var x = left; x < right; x++)
                {
                    if (!mask[y * width + x])
                        continue;
                    top = Mathf.Max(top, y);
                    bottom = Mathf.Min(bottom, y);
                    first = Mathf.Min(first, x);
                    last = Mathf.Max(last, x);
                }
            }
            if (top < 0)
                return;
            var span = Mathf.Max(1, top - bottom);
            var cell = new CloakCell(left, right, top, bottom, first, Mathf.Max(1, last - first), phase);
            for (var y = bottom; y <= top; y++)
            {
                for (var x = left; x < right; x++)
                {
                    var index = y * width + x;
                    if (!mask[index])
                        continue;
                    var pixel = pixels[index];
                    var fromTop = top - y;
                    if (cloak.Pattern >= CloakPattern.Pearl)
                        pixels[index] = Living(cloak, pixels, mask, width, height, cell, x, y);
                    else if (cloak.Pattern == CloakPattern.Gradient)
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

        private static Color32 Living(CloakPaint cloak, Color32[] pixels, bool[] mask, int width, int height, CloakCell cell, int x, int y)
        {
            var pixel = pixels[y * width + x];
            var across = x - cell.Left;
            var fromTop = cell.Top - y;
            var fromBottom = y - cell.Bottom;
            var span = Mathf.Max(1, cell.Top - cell.Bottom);
            var phase = cell.Phase;
            switch (cloak.Pattern)
            {
                case CloakPattern.Pearl:
                    return Shifted(pixel, Cycle(cloak, phase + (across + fromTop) / PearlPeriod + (Bright(pixel) ? PearlSheenShift : 0f)), CloakReference);
                case CloakPattern.Aurora:
                    return Shifted(pixel, Cycle(cloak, fromTop / AuroraPeriod - phase + Mathf.Sin(across * AuroraSway) * AuroraSwayDepth), CloakReference);
                case CloakPattern.Embers:
                    return Ember(cloak, mask, width, cell, pixel, x, y, across, fromBottom, span);
                case CloakPattern.Twinkle:
                    return Twinkle(cloak, cell, pixel, x, across, fromTop, span);
                case CloakPattern.Lightning:
                    return Bolt(cloak, cell, pixel, x, fromTop);
                case CloakPattern.Frost:
                    return Rime(cloak, cell, pixel, across, fromTop, fromBottom, span);
                case CloakPattern.Ripple:
                    var wave = 0.5f + 0.5f * Mathf.Sin(Turn * (phase + fromTop / RipplePeriod) + Mathf.Sin(across * RippleSway + Turn * phase) * RippleDepth);
                    return Shifted(pixel, wave > RippleCrest ? cloak.Third : Color.Lerp(cloak.First, cloak.Second, wave), CloakReference);
                default:
                    return Stitch(cloak, pixels, width, height, cell, pixel, x, y, across, fromTop, span);
            }
        }

        private static Color Cycle(CloakPaint cloak, float at)
        {
            var turn = (at - Mathf.Floor(at)) * 3f;
            if (turn < 1f)
                return Color.Lerp(cloak.First, cloak.Second, turn);
            return turn < 2f ? Color.Lerp(cloak.Second, cloak.Third, turn - 1f) : Color.Lerp(cloak.Third, cloak.First, turn - 2f);
        }

        private static Color32 Ember(CloakPaint cloak, bool[] mask, int width, CloakCell cell, Color32 pixel, int x, int y, int across, int fromBottom, int span)
        {
            var heat = 0.5f + 0.5f * Mathf.Sin(Turn * (cell.Phase + across / EmberPeriod));
            var low = fromBottom * 2 < span;
            if (low && !Cloak(mask, width, cell, x, y - 1))
                return Solid(heat < 0.5f ? Color.Lerp(cloak.First, cloak.Second, heat * 2f) : Color.Lerp(cloak.Second, cloak.Third, heat * 2f - 1f), pixel.a);
            if (Hash(across, EmberEvery) % EmberEvery == 0)
            {
                var rise = cell.Phase + Hash(across, EmberRise) % EmberRise / (float)EmberRise;
                rise -= Mathf.Floor(rise);
                if (fromBottom == 2 + (int)(rise * EmberRise))
                    return Solid(rise < 0.5f ? cloak.Third : cloak.Second, pixel.a);
            }
            if (low && !Cloak(mask, width, cell, x, y - 2))
                return Shifted(pixel, Color.Lerp(cloak.First, cloak.Second, heat * 0.6f), CloakReference);
            return Shifted(pixel, cloak.First, CloakReference);
        }

        private static Color32 Twinkle(CloakPaint cloak, CloakCell cell, Color32 pixel, int x, int across, int fromTop, int span)
        {
            if (cell.Phase >= ShootFrom && cell.Phase < ShootTo)
            {
                var head = (int)((cell.Phase - ShootFrom) / (ShootTo - ShootFrom) * (span + ShootTail));
                var path = cell.First + (int)(cell.Width * 0.15f + fromTop * ShootSlope);
                if (x == path && fromTop <= head && fromTop > head - ShootTail)
                    return Solid(fromTop == head ? cloak.Second : cloak.Third, pixel.a);
            }
            var seed = Hash(across, fromTop);
            if (seed % StarEvery != 0)
                return Shifted(pixel, cloak.First, CloakReference);
            var own = cell.Phase + seed / StarEvery % TwinkleSteps / (float)TwinkleSteps;
            own -= Mathf.Floor(own);
            if (own < 0.5f)
                return Solid(cloak.Second, pixel.a);
            return own < 0.75f ? Solid(Color.Lerp(cloak.First, cloak.Third, 0.6f), pixel.a) : Shifted(pixel, cloak.First, CloakReference);
        }

        private static Color32 Bolt(CloakPaint cloak, CloakCell cell, Color32 pixel, int x, int fromTop)
        {
            var second = cell.Phase >= SecondBoltAt;
            var since = second ? cell.Phase - SecondBoltAt : cell.Phase;
            if (since >= BoltSeconds * 2f)
                return Shifted(pixel, cloak.First, CloakReference);
            var path = cell.First + (int)(cell.Width * (second ? 0.7f : 0.3f) + fromTop * (second ? -BoltSlope : BoltSlope)) + Hash(fromTop / 2, second ? 5 : 3) % 3 - 1;
            var away = Mathf.Abs(x - path);
            if (since >= BoltSeconds)
                return Shifted(pixel, away == 0 ? cloak.Third : Color.Lerp(cloak.First, cloak.Third, 0.15f), CloakReference);
            if (away == 0)
                return Solid(cloak.Second, pixel.a);
            return Shifted(pixel, Color.Lerp(cloak.First, cloak.Third, away == 1 ? 0.8f : 0.3f), CloakReference);
        }

        private static Color32 Rime(CloakPaint cloak, CloakCell cell, Color32 pixel, int across, int fromTop, int fromBottom, int span)
        {
            var growing = cell.Phase < FrostGrowUntil;
            var share = growing ? cell.Phase / FrostGrowUntil : 1f - (cell.Phase - FrostGrowUntil) / (1f - FrostGrowUntil);
            var line = FrostReach * span * share * (2f - share) + Hash(across, FrostGlitterEvery) % 3 - 1;
            if (!growing && fromBottom < FrostReach * span && Hash(across * 31 + fromTop, (int)(cell.Phase * FrostGlitterSteps)) % FrostGlitterEvery == 0)
                return Solid(cloak.Third, pixel.a);
            if (fromBottom >= line)
                return Shifted(pixel, cloak.First, CloakReference);
            return fromBottom >= line - 1f ? Solid(cloak.Second, pixel.a) : Shifted(pixel, Color.Lerp(cloak.First, cloak.Second, 0.7f), CloakReference);
        }

        private static Color32 Stitch(CloakPaint cloak, Color32[] pixels, int width, int height, CloakCell cell, Color32 pixel, int x, int y, int across, int fromTop, int span)
        {
            var sewn = (across + fromTop) % ThreadStep == 0 || ((across - fromTop) % ThreadStep + ThreadStep) % ThreadStep == 0
                || TakesSecond(CloakPattern.Trim, pixels, width, height, cell.Left, cell.Right, x, y, fromTop);
            if (!sewn)
                return Shifted(pixel, cloak.First, CloakReference);
            var along = (fromTop + x - cell.First) / (float)(span + cell.Width);
            var glint = cell.Phase / GlintShare * (1f + 2f * GlintWidth) - GlintWidth;
            return Mathf.Abs(along - glint) < GlintWidth ? Solid(cloak.Third, pixel.a) : Shifted(pixel, cloak.Second, CloakReference);
        }

        private static bool Cloak(bool[] mask, int width, CloakCell cell, int x, int y)
        {
            return y >= 0 && x >= cell.Left && x < cell.Right && mask[y * width + x];
        }

        private static bool Bright(Color32 pixel) => Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > HighlightAbove;

        private static int Hash(int a, int b) => ((a * 73856093) ^ (b * 19349663)) & int.MaxValue;

        private readonly struct CloakCell
        {
            public CloakCell(int left, int right, int top, int bottom, int first, int width, float phase)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
                First = first;
                Width = width;
                Phase = phase;
            }

            public int Left { get; }
            public int Right { get; }
            public int Top { get; }
            public int Bottom { get; }
            public int First { get; }
            public int Width { get; }
            public float Phase { get; }
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
