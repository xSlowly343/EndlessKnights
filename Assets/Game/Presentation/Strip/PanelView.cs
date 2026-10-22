using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class PanelView : MonoBehaviour
    {
        public const float PanelWidthPx = 430f;
        public const float PanelHeightPx = 560f;
        public const float GapPx = 16f;
        public const int PanelCount = 3;

        public static readonly float AreaWidthPx = PanelCount * PanelWidthPx + (PanelCount + 1) * GapPx;
        public static readonly float AreaHeightPx = PanelHeightPx + 2 * GapPx;

        private static readonly Color FrameColor = new Color(0.12f, 0.1f, 0.11f, 1f);
        private static readonly Color TitleColor = new Color(0.62f, 0.12f, 0.12f, 1f);
        private static readonly Color CloseColor = new Color(0.45f, 0.08f, 0.08f, 1f);
        private static readonly string[] Titles = { "Статистика", "Герой", "Дерево" };

        private readonly List<StripButton> _closeButtons = new List<StripButton>();
        private readonly List<StripButton> _bodies = new List<StripButton>();

        public event Action? CloseRequested;

        public static PanelView Create()
        {
            var host = new GameObject("Panels");
            var view = host.AddComponent<PanelView>();
            view.Build();
            host.SetActive(false);
            return view;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Show(bool aboveStrip)
        {
            Arrange(aboveStrip);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsOpen)
                return false;
            foreach (var close in _closeButtons)
            {
                if (close.Contains(worldPoint))
                {
                    CloseRequested?.Invoke();
                    return true;
                }
            }
            foreach (var body in _bodies)
                if (body.Contains(worldPoint))
                    return true;
            return false;
        }

        private void Build()
        {
            foreach (var title in Titles)
            {
                var panel = new GameObject("Panel " + title).transform;
                panel.SetParent(transform, false);
                var body = StripButton.Create("Body", FrameColor, Vector2.zero, Vector2.one, panel);
                body.GetComponent<SpriteRenderer>().sortingOrder = 5;
                var titleBar = StripButton.Create("Title", TitleColor, Vector2.zero, Vector2.one, panel);
                titleBar.GetComponent<SpriteRenderer>().sortingOrder = 6;
                var close = StripButton.Create("Close", CloseColor, Vector2.zero, Vector2.one, panel);
                close.GetComponent<SpriteRenderer>().sortingOrder = 7;
                StripText.Create("TitleText", title, Vector2.zero, 0.22f, TextAnchor.MiddleCenter, Color.white, panel, 8);
                StripText.Create("CloseText", "X", Vector2.zero, 0.22f, TextAnchor.MiddleCenter, Color.white, panel, 8);
                _bodies.Add(body);
                _closeButtons.Add(close);
            }
        }

        private void Arrange(bool aboveStrip)
        {
            var pixelsPerUnit = StripSpace.BaseHeightPx / StripSpace.Height;
            var width = PanelWidthPx / pixelsPerUnit;
            var height = PanelHeightPx / pixelsPerUnit;
            var gap = GapPx / pixelsPerUnit;
            var titleHeight = 40f / pixelsPerUnit;
            var closeSize = 30f / pixelsPerUnit;
            var bottom = aboveStrip ? StripSpace.Height + gap : -gap - height;

            for (var i = 0; i < PanelCount; i++)
            {
                var panel = transform.GetChild(i);
                var left = gap + i * (width + gap);
                var centre = new Vector2(left + width / 2f, bottom + height / 2f);
                Place(panel.Find("Body"), centre, new Vector2(width, height));
                var titleCentre = new Vector2(centre.x, bottom + height - titleHeight / 2f);
                Place(panel.Find("Title"), titleCentre, new Vector2(width * 0.6f, titleHeight));
                var closeCentre = new Vector2(left + width - closeSize, bottom + height - titleHeight / 2f);
                Place(panel.Find("Close"), closeCentre, new Vector2(closeSize, closeSize));
                panel.Find("TitleText").position = titleCentre;
                panel.Find("CloseText").position = closeCentre;
            }
        }

        private static void Place(Transform item, Vector2 centre, Vector2 size)
        {
            item.position = new Vector3(centre.x, centre.y, item.position.z);
            item.localScale = new Vector3(size.x, size.y, 1f);
        }
    }
}
