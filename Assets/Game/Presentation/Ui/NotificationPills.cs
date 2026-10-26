using System.Collections.Generic;
using Game.Core.Ui;
using Game.Presentation.Strip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class NotificationPills : MonoBehaviour
    {
        public const float PillHeightPx = 26f;
        public const float SpacingPx = 4f;
        public const float AreaHeightPx = NotificationQueue.MaxVisible * PillHeightPx + (NotificationQueue.MaxVisible - 1) * SpacingPx;

        private const float RadiusPx = 13f;
        private const float DotPx = 6f;
        private const int PaddingPx = 12;
        private const float TextSizePx = 13f;
        private const float FadeInSeconds = 0.15f;
        private const float FadeOutSeconds = 0.4f;

        private readonly List<CanvasGroup> _pills = new List<CanvasGroup>();
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
        private RectTransform _canvasRect = null!;

        public int ShownCount { get; private set; }

        public string TextAt(int index) => _labels[index].text;

        public static NotificationPills Create()
        {
            var host = new GameObject("Notifications");
            var view = host.AddComponent<NotificationPills>();
            view.Build(UiTheme.Load());
            host.SetActive(false);
            return view;
        }

        public void Place(bool aboveStrip)
        {
            var gap = StripSpace.Units(PanelGeometry.GapToStripPx);
            _canvasRect.pivot = aboveStrip ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            _canvasRect.position = new Vector3(StripSpace.Width / 2f, aboveStrip ? StripSpace.Height + gap : -gap, 0f);
            var column = _canvasRect.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = aboveStrip ? TextAnchor.LowerCenter : TextAnchor.UpperCenter;
            column.reverseArrangement = !aboveStrip;
        }

        public void Show(IReadOnlyList<Notification> items, double now)
        {
            ShownCount = items.Count;
            for (var i = 0; i < _pills.Count; i++)
            {
                var shown = i < items.Count;
                _pills[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var item = items[i];
                if (_labels[i].text != item.Text)
                    _labels[i].text = item.Text;
                _pills[i].alpha = Alpha(item.Age(now));
            }
            gameObject.SetActive(items.Count > 0);
        }

        public void Hide()
        {
            ShownCount = 0;
            gameObject.SetActive(false);
        }

        private static float Alpha(double age)
        {
            var fadeIn = (float)(age / FadeInSeconds);
            var fadeOut = (float)((NotificationQueue.LifetimeSeconds - age) / FadeOutSeconds);
            return Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));
        }

        private void Build(UiTheme theme)
        {
            _canvasRect = UiElements.Rect("Canvas", transform);
            var canvas = _canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = PanelCanvas.SortingOrder;
            _canvasRect.localScale = Vector3.one / StripSpace.PixelsPerUnit;
            _canvasRect.sizeDelta = new Vector2(StripSpace.BaseWidthPx, AreaHeightPx);
            var column = _canvasRect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = SpacingPx;
            column.childAlignment = TextAnchor.LowerCenter;
            column.childControlWidth = true;
            column.childControlHeight = false;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            for (var i = 0; i < NotificationQueue.MaxVisible; i++)
                AddPill(theme, i);
        }

        private void AddPill(UiTheme theme, int index)
        {
            var back = UiElements.Fill("Pill " + index, _canvasRect, theme.Background, RadiusPx);
            back.rectTransform.sizeDelta = new Vector2(0f, PillHeightPx);
            var outline = back.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.Gold;
            outline.effectDistance = new Vector2(1f, -1f);
            var row = back.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(PaddingPx, PaddingPx + 2, 0, 0);
            row.spacing = PaddingPx / 2f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var dot = UiElements.Dot("Dot", back.rectTransform, theme.Gold);
            var dotSize = dot.gameObject.AddComponent<LayoutElement>();
            dotSize.preferredWidth = DotPx;
            dotSize.preferredHeight = DotPx;
            var label = UiElements.Label("Text", back.rectTransform, theme.TextFont, TextSizePx, theme.Text, TextAlignmentOptions.MidlineLeft);
            _pills.Add(back.gameObject.AddComponent<CanvasGroup>());
            _labels.Add(label);
            back.gameObject.SetActive(false);
        }
    }
}
