using System;
using System.Collections.Generic;
using Game.Presentation.Tree;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class PanelView : MonoBehaviour
    {
        public const float PanelWidthPx = 430f;
        public const float PanelHeightPx = 560f;
        public const float GapPx = 16f;
        public const int PanelCount = 3;
        public const float ReportHeightPx = 340f;
        public const int MaxReportLines = 7;
        private const int TreePanelIndex = 2;

        public static readonly float AreaWidthPx = PanelCount * PanelWidthPx + (PanelCount + 1) * GapPx;
        public static readonly float AreaHeightPx = PanelHeightPx + 2 * GapPx;
        public static readonly float ReportAreaWidthPx = PanelWidthPx + 2 * GapPx;
        public static readonly float ReportAreaHeightPx = ReportHeightPx + 2 * GapPx;

        private static readonly Color FrameColor = new Color(0.12f, 0.1f, 0.11f, 1f);
        private static readonly Color TitleColor = new Color(0.62f, 0.12f, 0.12f, 1f);
        private static readonly Color CloseColor = new Color(0.45f, 0.08f, 0.08f, 1f);
        private static readonly Color ClaimColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        private static readonly Color ZoomColor = new Color(0.35f, 0.1f, 0.1f, 1f);
        private static readonly Color LineColor = new Color(0.92f, 0.9f, 0.85f, 1f);
        private static readonly string[] Titles = { "Статистика", "Герой", "Дерево" };

        private readonly List<StripButton> _closeButtons = new List<StripButton>();
        private readonly List<StripButton> _bodies = new List<StripButton>();
        private readonly List<TextMesh> _reportLines = new List<TextMesh>();
        private Transform _report = null!;
        private StripButton _reportBody = null!;
        private StripButton _claimButton = null!;
        private TextMesh _reportTitle = null!;
        private StripButton _zoomIn = null!;
        private StripButton _zoomOut = null!;
        private bool _treeCaptured;

        public TreePanel Tree { get; private set; } = null!;

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
            SetPanelsVisible(true);
            Arrange(aboveStrip);
            gameObject.SetActive(true);
        }

        public void ShowReport(string title, IReadOnlyList<string> lines, bool aboveStrip)
        {
            SetPanelsVisible(false);
            _reportTitle.text = title;
            for (var i = 0; i < _reportLines.Count; i++)
                _reportLines[i].text = i < lines.Count ? lines[i] : string.Empty;
            ArrangeReport(aboveStrip);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsOpen)
                return false;
            if (_claimButton.Contains(worldPoint))
            {
                CloseRequested?.Invoke();
                return true;
            }
            if (_reportBody.Contains(worldPoint))
                return true;
            if (_zoomIn.Contains(worldPoint))
            {
                Tree.ZoomIn();
                return true;
            }
            if (_zoomOut.Contains(worldPoint))
            {
                Tree.ZoomOut();
                return true;
            }
            foreach (var close in _closeButtons)
            {
                if (close.Contains(worldPoint))
                {
                    CloseRequested?.Invoke();
                    return true;
                }
            }
            if (Tree.isActiveAndEnabled && Tree.TryPress(worldPoint))
            {
                _treeCaptured = true;
                return true;
            }
            foreach (var body in _bodies)
                if (body.Contains(worldPoint))
                    return true;
            return false;
        }

        public void Drag(Vector2 worldPoint)
        {
            if (_treeCaptured)
                Tree.Drag(worldPoint);
        }

        public void Release(Vector2 worldPoint)
        {
            if (!_treeCaptured)
                return;
            _treeCaptured = false;
            Tree.Release(worldPoint);
        }

        public void CancelPress()
        {
            _treeCaptured = false;
            Tree.CancelPress();
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
            BuildTreeControls();
            BuildReport();
        }

        private void BuildTreeControls()
        {
            var panel = transform.GetChild(TreePanelIndex);
            Tree = TreePanel.Create(panel);
            _zoomOut = StripButton.Create("ZoomOut", ZoomColor, Vector2.zero, Vector2.one, panel);
            _zoomOut.GetComponent<SpriteRenderer>().sortingOrder = 7;
            _zoomIn = StripButton.Create("ZoomIn", ZoomColor, Vector2.zero, Vector2.one, panel);
            _zoomIn.GetComponent<SpriteRenderer>().sortingOrder = 7;
            StripText.Create("ZoomOutText", "−", Vector2.zero, 0.22f, TextAnchor.MiddleCenter, Color.white, panel, 8);
            StripText.Create("ZoomInText", "+", Vector2.zero, 0.22f, TextAnchor.MiddleCenter, Color.white, panel, 8);
        }

        private void BuildReport()
        {
            _report = new GameObject("Report").transform;
            _report.SetParent(transform, false);
            _reportBody = StripButton.Create("Body", FrameColor, Vector2.zero, Vector2.one, _report);
            _reportBody.GetComponent<SpriteRenderer>().sortingOrder = 5;
            var titleBar = StripButton.Create("Title", TitleColor, Vector2.zero, Vector2.one, _report);
            titleBar.GetComponent<SpriteRenderer>().sortingOrder = 6;
            _claimButton = StripButton.Create("Claim", ClaimColor, Vector2.zero, Vector2.one, _report);
            _claimButton.GetComponent<SpriteRenderer>().sortingOrder = 6;
            _reportTitle = StripText.Create("TitleText", string.Empty, Vector2.zero, 0.22f, TextAnchor.MiddleCenter, Color.white, _report, 8);
            StripText.Create("ClaimText", "Забрать", Vector2.zero, 0.22f, TextAnchor.MiddleCenter, Color.white, _report, 8);
            for (var i = 0; i < MaxReportLines; i++)
                _reportLines.Add(StripText.Create("Line " + i, string.Empty, Vector2.zero, 0.17f, TextAnchor.MiddleLeft, LineColor, _report, 8));
        }

        private void SetPanelsVisible(bool visible)
        {
            for (var i = 0; i < PanelCount; i++)
                transform.GetChild(i).gameObject.SetActive(visible);
            _report.gameObject.SetActive(!visible);
        }

        private void ArrangeReport(bool aboveStrip)
        {
            var pixelsPerUnit = StripSpace.BaseHeightPx / StripSpace.Height;
            var width = PanelWidthPx / pixelsPerUnit;
            var height = ReportHeightPx / pixelsPerUnit;
            var gap = GapPx / pixelsPerUnit;
            var titleHeight = 40f / pixelsPerUnit;
            var lineHeight = 32f / pixelsPerUnit;
            var claimSize = new Vector2(160f / pixelsPerUnit, 40f / pixelsPerUnit);
            var bottom = aboveStrip ? StripSpace.Height + gap : -gap - height;
            var centre = new Vector2(gap + width / 2f, bottom + height / 2f);
            Place(_report.Find("Body"), centre, new Vector2(width, height));
            var titleCentre = new Vector2(centre.x, bottom + height - titleHeight / 2f);
            Place(_report.Find("Title"), titleCentre, new Vector2(width, titleHeight));
            _report.Find("TitleText").position = titleCentre;
            var claimCentre = new Vector2(centre.x, bottom + gap + claimSize.y / 2f);
            Place(_report.Find("Claim"), claimCentre, claimSize);
            _report.Find("ClaimText").position = claimCentre;
            var firstLineY = bottom + height - titleHeight - gap - lineHeight / 2f;
            for (var i = 0; i < _reportLines.Count; i++)
                _reportLines[i].transform.position = new Vector2(2f * gap, firstLineY - i * lineHeight);
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
                if (i != TreePanelIndex)
                    continue;
                var zoomOutCentre = new Vector2(left + closeSize, closeCentre.y);
                var zoomInCentre = new Vector2(left + closeSize * 2.4f, closeCentre.y);
                Place(panel.Find("ZoomOut"), zoomOutCentre, new Vector2(closeSize, closeSize));
                Place(panel.Find("ZoomIn"), zoomInCentre, new Vector2(closeSize, closeSize));
                panel.Find("ZoomOutText").position = zoomOutCentre;
                panel.Find("ZoomInText").position = zoomInCentre;
                Tree.Arrange(left, bottom, width, height - titleHeight, pixelsPerUnit);
            }
        }

        private static void Place(Transform item, Vector2 centre, Vector2 size)
        {
            item.position = new Vector3(centre.x, centre.y, item.position.z);
            item.localScale = new Vector3(size.x, size.y, 1f);
        }
    }
}
