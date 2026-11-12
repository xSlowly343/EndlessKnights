using System;
using System.Collections.Generic;
using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class MailPanel
    {
        public const int MaxRows = 6;

        private const float PaddingPx = 12f;
        private const float TopPx = 4f;
        private const float GapPx = 6f;
        private const float RadiusPx = 8f;
        private const float RowPx = 64f;
        private const float ClaimWidthPx = 88f;
        private const float ClaimHeightPx = 28f;
        private const float EmptyPx = 60f;
        private const float PagerPx = 24f;
        private const float TurnWidthPx = 44f;
        private const float PageLabelWidthPx = 96f;

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<LetterRow> _rows = new List<LetterRow>();
        private readonly TextMeshProUGUI _empty;
        private readonly RectTransform _pager;
        private readonly UiButton _previous;
        private readonly UiButton _next;
        private readonly TextMeshProUGUI _pageLabel;
        private IReadOnlyList<MailRow> _letters = Array.Empty<MailRow>();
        private float _contentPx = PanelGeometry.FullHeightPx;

        private MailPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Mail", content));
            for (var i = 0; i < MaxRows; i++)
                _rows.Add(LetterRow.Create(i, _root, this));
            _empty = UiElements.Label("Empty", _root, theme.TextFont, 12f, theme.TextDim, TextAlignmentOptions.Center);
            _empty.textWrappingMode = TextWrappingModes.Normal;
            _pager = UiElements.Rect("Pager", _root);
            _previous = Button(_pager, "‹", theme.Card, theme.Text, theme.TextDim, 14f);
            UiElements.Anchor(_previous.Rect, new Vector2(0.5f, 0.5f), new Vector2(-(PageLabelWidthPx + TurnWidthPx) / 2f, 0f), new Vector2(TurnWidthPx, PagerPx));
            _next = Button(_pager, "›", theme.Card, theme.Text, theme.TextDim, 14f);
            UiElements.Anchor(_next.Rect, new Vector2(0.5f, 0.5f), new Vector2((PageLabelWidthPx + TurnWidthPx) / 2f, 0f), new Vector2(TurnWidthPx, PagerPx));
            _pageLabel = UiElements.Label("Page", _pager, theme.TextFont, 12f, theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(_pageLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PageLabelWidthPx, PagerPx));
            _pager.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        public static MailPanel Create(RectTransform content, UiTheme theme) => new MailPanel(content, theme);

        public event Action<string>? ClaimRequested;
        public event Action? Opened;

        public bool IsShown => _root.gameObject.activeSelf;
        public int ShownRows { get; private set; }
        public int Page { get; private set; }
        public int Pages { get; private set; } = 1;

        public void Show(bool shown, float contentPx)
        {
            var opened = shown && !IsShown;
            _root.gameObject.SetActive(shown);
            _contentPx = contentPx;
            if (!shown)
                return;
            Refresh();
            if (opened)
                Opened?.Invoke();
        }

        public void ShowRows(IReadOnlyList<MailRow> rows)
        {
            _letters = rows;
            Refresh();
        }

        public void Turn(int pages)
        {
            Page += pages;
            Refresh();
        }

        private void Refresh()
        {
            var page = MailText.Page(_letters.Count, PerPage(), Page);
            Page = page.Index;
            Pages = page.Pages;
            ShownRows = page.Count;
            for (var i = 0; i < _rows.Count; i++)
            {
                var shown = i < page.Count;
                _rows[i].Show(shown ? _letters[page.First + i] : default, shown, _theme);
            }
            _empty.text = _letters.Count == 0 ? MailText.Empty : string.Empty;
            _pager.gameObject.SetActive(page.Pages > 1);
            _pageLabel.text = page.Label;
            Dim(_previous, !page.HasPrevious);
            Dim(_next, !page.HasNext);
            Arrange(page.Count);
        }

        private int PerPage()
        {
            var whole = Fitting(_contentPx);
            return System.Math.Max(1, _letters.Count <= whole ? whole : Fitting(_contentPx - PagerPx - GapPx));
        }

        private static int Fitting(float heightPx) =>
            Mathf.Clamp(Mathf.FloorToInt((heightPx - TopPx + GapPx) / (RowPx + GapPx)), 0, MaxRows);

        private void Dim(UiButton button, bool dimmed)
        {
            if (button.Label != null)
                button.Label.color = dimmed ? _theme.TextDim : _theme.Text;
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown)
                return false;
            if (Pages > 1 && (_previous.Contains(worldPoint) || _next.Contains(worldPoint)))
            {
                Turn(_previous.Contains(worldPoint) ? -1 : 1);
                return true;
            }
            foreach (var row in _rows)
            {
                if (!row.ClaimContains(worldPoint))
                    continue;
                ClaimRequested?.Invoke(row.LetterId);
                return true;
            }
            return false;
        }

        public Vector2 WorldPointOfClaim(int row) => _rows[row].ClaimPoint;

        public Vector2 WorldPointOfTurn(int pages) => pages < 0 ? Centre(_previous.Rect) : Centre(_next.Rect);

        private static Vector2 Centre(RectTransform rect) => rect.TransformPoint(rect.rect.center);

        private void Arrange(int shown)
        {
            var y = TopPx;
            for (var i = 0; i < _rows.Count; i++)
            {
                var fits = i < shown;
                _rows[i].Fit(fits);
                if (!fits)
                    continue;
                Place(_rows[i].Rect, y, RowPx);
                y += RowPx + GapPx;
            }
            Place(_pager, y, PagerPx);
            Place(_empty.rectTransform, TopPx, EmptyPx);
        }

        private UiButton Button(RectTransform parent, string text, Color fill, Color textColor, Color border, float sizePx)
        {
            var back = UiElements.Fill(text, parent, border, RadiusPx);
            UiElements.Stretch(UiElements.Raise(UiElements.Fill("Inner", back.rectTransform, fill, RadiusPx - PanelCanvas.BorderPx)).rectTransform, PanelCanvas.BorderPx);
            var label = UiElements.Label("Text", back.rectTransform, _theme.BoldFont, sizePx, textColor, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform, 4f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = sizePx;
            label.text = text;
            return new UiButton(back, label);
        }

        private static void Place(RectTransform rect, float y, float height) =>
            UiElements.Anchor(rect, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(Width(), height));

        private static float Width() => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;

        private sealed class LetterRow
        {
            private readonly Image _back;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _body;
            private readonly TextMeshProUGUI _reward;
            private readonly UiButton _claim;
            private bool _shown;
            private bool _fits = true;
            private bool _claimed;

            private LetterRow(Image back, TextMeshProUGUI title, TextMeshProUGUI body, TextMeshProUGUI reward, UiButton claim)
            {
                _back = back;
                _title = title;
                _body = body;
                _reward = reward;
                _claim = claim;
                LetterId = string.Empty;
            }

            public RectTransform Rect => _back.rectTransform;
            public string LetterId { get; private set; }
            public Vector2 ClaimPoint => _claim.Rect.TransformPoint(_claim.Rect.rect.center);

            public static LetterRow Create(int index, RectTransform parent, MailPanel panel)
            {
                var theme = panel._theme;
                var back = UiElements.Fill("Letter " + index, parent, theme.Card, RadiusPx);
                var rect = back.rectTransform;
                var textWidth = Width() - ClaimWidthPx - 3f * PaddingPx;
                var title = UiElements.Label("Title", rect, theme.BoldFont, 14f, theme.Text, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -6f), new Vector2(textWidth, 18f));
                title.overflowMode = TextOverflowModes.Ellipsis;
                var body = UiElements.Label("Body", rect, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(body.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -25f), new Vector2(textWidth, 14f));
                body.overflowMode = TextOverflowModes.Ellipsis;
                var reward = UiElements.Label("Reward", rect, theme.TextFont, 11f, theme.Gold, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(reward.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -42f), new Vector2(textWidth, 14f));
                reward.overflowMode = TextOverflowModes.Ellipsis;
                var claim = panel.Button(rect, "Claim", theme.Gold, theme.TextOnGold, theme.Gold, 12f);
                UiElements.Anchor(claim.Rect, new Vector2(1f, 0.5f), new Vector2(-PaddingPx, 0f), new Vector2(ClaimWidthPx, ClaimHeightPx));
                back.gameObject.SetActive(false);
                return new LetterRow(back, title, body, reward, claim);
            }

            public void Show(MailRow row, bool shown, UiTheme theme)
            {
                _shown = shown;
                _back.gameObject.SetActive(_shown && _fits);
                if (!shown)
                {
                    LetterId = string.Empty;
                    return;
                }
                LetterId = row.Id;
                _claimed = row.Claimed;
                _title.text = row.Title;
                _body.text = row.Body;
                _reward.text = row.Reward;
                _claim.Text = row.Button;
                if (_claim.Label != null)
                    _claim.Label.color = row.Claimed ? theme.TextDim : theme.TextOnGold;
            }

            public void Fit(bool fits)
            {
                _fits = fits;
                _back.gameObject.SetActive(_shown && _fits);
            }

            public bool ClaimContains(Vector2 worldPoint) => _shown && _fits && !_claimed && LetterId.Length > 0 && _claim.Contains(worldPoint);
        }
    }
}
