using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public static class StripCanvas
    {
        public static RectTransform Create(string name, int sortingOrder, Transform? parent = null)
        {
            var host = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            if (parent != null)
                rect.SetParent(parent, false);
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(StripSpace.BaseWidthPx, StripSpace.BaseHeightPx);
            rect.localScale = Vector3.one / StripSpace.PixelsPerUnit;
            rect.localPosition = Vector3.zero;
            return rect;
        }

        public static RectTransform Place(RectTransform rect, float xPx, float yFromTopPx, Vector2 sizePx, Vector2 pivot)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(xPx, StripSpace.BaseHeightPx - yFromTopPx);
            rect.sizeDelta = sizePx;
            return rect;
        }

        public static Vector2 PxOf(Vector2 worldPoint) => worldPoint * StripSpace.PixelsPerUnit;
    }
}
