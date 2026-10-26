using Game.Core.Content.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class RarityFrame
    {
        private const float GlowPx = 4f;
        private const float OutlinePx = 2f;
        private const float StarPx = 3f;
        private const float GlowAlpha = 0.4f;

        private static readonly Color[] Colors =
        {
            new Color32(0x9A, 0xA3, 0xB5, 0xFF),
            new Color32(0xE6, 0xE9, 0xF0, 0xFF),
            new Color32(0x4F, 0xC2, 0x77, 0xFF),
            new Color32(0x5B, 0x9B, 0xFF, 0xFF),
            new Color32(0xB0, 0x7C, 0xFF, 0xFF),
            new Color32(0xFF, 0x9A, 0x3C, 0xFF),
            new Color32(0xFF, 0x5A, 0x5A, 0xFF),
            new Color32(0x3F, 0xE0, 0xD0, 0xFF),
            Color.white,
            new Color32(0x1E, 0x23, 0x70, 0xFF),
            new Color32(0xFF, 0xF4, 0xD6, 0xFF),
        };

        private static readonly Color AstralRim = new Color32(0x5C, 0x66, 0xD6, 0xFF);
        private static readonly Color AstralIcon = new Color32(0xB8, 0xC2, 0xFF, 0xFF);
        private static readonly Color RiftGlow = new Color32(0xB0, 0x7C, 0xFF, 0xFF);
        private static readonly Color PrimordialRim = new Color(0.94f, 0.71f, 0.24f, 0.7f);

        private static readonly Vector2[] StarSpots =
        {
            new Vector2(0.2f, 1f),
            new Vector2(0.65f, 1f),
            new Vector2(1f, 0.3f),
            new Vector2(0.85f, 0f),
            new Vector2(0.4f, 0f),
            new Vector2(0f, 0.6f),
        };

        private readonly Image _glow;
        private readonly Image _outline;
        private readonly Image _frame;
        private readonly Image _inner;
        private readonly Image[] _stars;
        private readonly float _widthPx;

        private RarityFrame(Image glow, Image outline, Image frame, Image inner, Image[] stars, float widthPx)
        {
            _glow = glow;
            _outline = outline;
            _frame = frame;
            _inner = inner;
            _stars = stars;
            _widthPx = widthPx;
        }

        public RectTransform Content => _inner.rectTransform;

        public static RarityFrame Create(RectTransform host, float radiusPx, float widthPx, Color fill)
        {
            var glow = UiElements.Fill("Glow", host, Color.clear, radiusPx + GlowPx);
            UiElements.Stretch(glow.rectTransform, -GlowPx);
            var outline = UiElements.Fill("Outline", host, Color.clear, radiusPx + OutlinePx);
            UiElements.Stretch(outline.rectTransform, -OutlinePx);
            var frame = UiElements.Fill("Frame", host, Color.white, radiusPx);
            UiElements.Stretch(frame.rectTransform);
            var inner = UiElements.Fill("Inner", frame.rectTransform, fill, Mathf.Max(1f, radiusPx - widthPx));
            UiElements.Stretch(inner.rectTransform, widthPx);
            var stars = new Image[StarSpots.Length];
            for (var i = 0; i < stars.Length; i++)
                stars[i] = Star(frame.rectTransform, StarSpots[i], widthPx + 1f);
            return new RarityFrame(glow, outline, frame, inner, stars, widthPx);
        }

        public static Color Of(ItemRarity rarity) => Colors[(int)rarity];

        public static Color IconOf(ItemRarity rarity) =>
            rarity == ItemRarity.Astral ? AstralIcon : Of(rarity);

        public static Color TitleOf(ItemRarity rarity) => Color.Lerp(IconOf(rarity), Color.white, 0.45f);

        public static bool Glows(ItemRarity rarity) => rarity >= ItemRarity.Legendary;

        public void ShowEmpty(Color color)
        {
            ShowFrame(color, UiSprites.Rounded, _widthPx);
            _glow.color = Color.clear;
            _outline.color = Color.clear;
            ShowStars(false);
        }

        public void Show(ItemRarity rarity, bool glow)
        {
            var astral = rarity == ItemRarity.Astral;
            var rift = rarity == ItemRarity.Rift;
            ShowFrame(Of(rarity), rift ? UiSprites.RainbowRounded : UiSprites.Rounded, astral ? _widthPx + 1f : _widthPx);
            _glow.color = glow && Glows(rarity) ? Faded(GlowOf(rarity), GlowAlpha) : Color.clear;
            _outline.color = astral ? AstralRim : rarity == ItemRarity.Primordial ? PrimordialRim : Color.clear;
            ShowStars(astral);
        }

        private void ShowFrame(Color color, Sprite sprite, float widthPx)
        {
            _frame.color = color;
            _frame.sprite = sprite;
            UiElements.Stretch(_inner.rectTransform, widthPx);
        }

        private void ShowStars(bool shown)
        {
            foreach (var star in _stars)
                star.gameObject.SetActive(shown);
        }

        private static Color GlowOf(ItemRarity rarity) =>
            rarity == ItemRarity.Rift ? RiftGlow : rarity == ItemRarity.Astral ? AstralRim : Of(rarity);

        private static Color Faded(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Image Star(RectTransform frame, Vector2 spot, float bandPx)
        {
            var star = UiElements.Dot("Star", frame, Color.white);
            var rect = star.rectTransform;
            rect.anchorMin = spot;
            rect.anchorMax = spot;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * StarPx;
            rect.anchoredPosition = new Vector2(Inward(spot.x, bandPx), Inward(spot.y, bandPx));
            star.gameObject.SetActive(false);
            return star;
        }

        private static float Inward(float edge, float bandPx) =>
            edge <= 0f ? bandPx / 2f : edge >= 1f ? -bandPx / 2f : 0f;
    }
}
