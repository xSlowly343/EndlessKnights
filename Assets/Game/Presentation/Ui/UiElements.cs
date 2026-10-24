using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public static class UiElements
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.layer = parent.gameObject.layer;
            var rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float insetPx = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(insetPx, insetPx);
            rect.offsetMax = new Vector2(-insetPx, -insetPx);
            return rect;
        }

        public static RectTransform Anchor(RectTransform rect, Vector2 anchor, Vector2 positionPx, Vector2 sizePx)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = positionPx;
            rect.sizeDelta = sizePx;
            return rect;
        }

        public static RectTransform TopEdge(RectTransform rect, float heightPx, float insetPx = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-2f * insetPx, heightPx);
            return rect;
        }

        public static RectTransform BottomEdge(RectTransform rect, float heightPx, float insetPx = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-2f * insetPx, heightPx);
            return rect;
        }

        public static Image Fill(string name, Transform parent, Color color, float radiusPx = 0f)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radiusPx <= 0f)
                return image;
            image.sprite = UiSprites.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = UiSprites.MultiplierFor(radiusPx);
            return image;
        }

        public static Image Dot(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Circle;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset? font, float sizePx, Color color, TextAlignmentOptions alignment)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.fontSize = sizePx;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static LayoutElement FixedHeight(RectTransform rect, float heightPx)
        {
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = heightPx;
            layout.preferredHeight = heightPx;
            return layout;
        }

        public static void IgnoreLayout(RectTransform rect) => rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        public static bool Contains(RectTransform rect, Vector2 worldPoint) =>
            rect.gameObject.activeInHierarchy && rect.rect.Contains((Vector2)rect.InverseTransformPoint(worldPoint));
    }
}
