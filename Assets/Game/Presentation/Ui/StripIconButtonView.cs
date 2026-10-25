using Game.Presentation.Strip;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class StripIconButtonView
    {
        public const float SizePx = 30f;

        private const int SortingOrder = 30;
        private const float RadiusPx = 8f;
        private const float BorderPx = 1f;
        private const float GlyphPx = 18f;

        private static readonly Color Fill = new Color32(0x2E, 0x3A, 0x58, 0xFF);

        private StripIconButtonView(RectTransform root, Image border, Image glyph)
        {
            Root = root;
            Border = border;
            Glyph = glyph;
        }

        public RectTransform Root { get; }
        public Image Border { get; }
        public Image Glyph { get; }

        public static StripIconButtonView Create(string name, Vector2 worldCentre, Sprite glyph, Color glyphColor, Color borderColor)
        {
            var host = new GameObject(name, typeof(RectTransform));
            var root = (RectTransform)host.transform;
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.one * SizePx;
            root.localScale = Vector3.one / StripSpace.PixelsPerUnit;
            root.position = new Vector3(worldCentre.x, worldCentre.y, 0f);
            host.AddComponent<BoxCollider2D>().size = Vector2.one * SizePx;
            host.AddComponent<StripClickable>();
            var border = UiElements.Fill("Border", root, borderColor, RadiusPx);
            UiElements.Stretch(border.rectTransform);
            UiElements.Stretch(UiElements.Fill("Fill", root, Fill, RadiusPx - BorderPx).rectTransform, BorderPx);
            var icon = UiElements.Rect("Glyph", root).gameObject.AddComponent<Image>();
            icon.sprite = glyph;
            icon.color = glyphColor;
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = Vector2.one * GlyphPx;
            return new StripIconButtonView(root, border, icon);
        }
    }
}
