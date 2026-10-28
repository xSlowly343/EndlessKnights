using System;
using System.Collections.Generic;
using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class PartySearchPanel
    {
        public const int MaxRows = 5;

        private const float PaddingPx = 12f;
        private const float TopPx = 4f;
        private const float GapPx = 6f;
        private const float RadiusPx = 8f;
        private const float QuickCardPx = 56f;
        private const float QuickButtonWidthPx = 96f;
        private const float ButtonPx = 30f;
        private const float FilterPx = 28f;
        private const float ListHeaderPx = 22f;
        private const float RefreshWidthPx = 96f;
        private const float RowPx = 56f;
        private const float JoinWidthPx = 88f;
        private const float JoinHeightPx = 26f;
        private const float SeatsWidthPx = 40f;
        private const float StatusPx = 60f;

        private static readonly Color FilterFill = new Color32(0x2E, 0x3A, 0x58, 0xFF);
        private static readonly Color TagText = new Color32(0xBF, 0xF2, 0xD2, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly UiButton _quick;
        private readonly UiButton[] _filters = new UiButton[3];
        private readonly TextMeshProUGUI _found;
        private readonly UiButton _refresh;
        private readonly List<ResultRow> _rows = new List<ResultRow>();
        private readonly TextMeshProUGUI _status;
        private PartySearchFilters? _searchFilters;
        private float _contentPx = PanelGeometry.FullHeightPx;

        private PartySearchPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("PartySearch", content));
            _quick = BuildQuickCard();
            for (var i = 0; i < _filters.Length; i++)
                _filters[i] = Button(_root, "Filter " + i, FilterFill, theme.Text, FilterFill, 11f);
            _found = UiElements.Label("Found", _root, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.MidlineLeft);
            _refresh = Button(_root, PartySearchTexts.Refresh, theme.Card, theme.Text, theme.Border, 11f);
            for (var i = 0; i < MaxRows; i++)
                _rows.Add(ResultRow.Create(i, _root, this));
            _status = UiElements.Label("Status", _root, theme.TextFont, 12f, theme.TextDim, TextAlignmentOptions.Center);
            _status.textWrappingMode = TextWrappingModes.Normal;
            _root.gameObject.SetActive(false);
        }

        public static PartySearchPanel Create(RectTransform content, UiTheme theme) => new PartySearchPanel(content, theme);

        public event Action? QuickRequested;
        public event Action? RefreshRequested;
        public event Action<string>? JoinRequested;

        public bool IsShown => _root.gameObject.activeSelf;
        public int ShownRows { get; private set; }
        public string StatusText => _status.text;

        public void UseFilters(PartySearchFilters filters)
        {
            _searchFilters = filters;
            ShowFilters();
        }

        public void Show(bool shown, float contentPx)
        {
            _root.gameObject.SetActive(shown);
            _contentPx = contentPx;
            if (shown)
                Arrange();
        }

        public void ShowRows(IReadOnlyList<PartySearchRow> rows)
        {
            ShownRows = 0;
            for (var i = 0; i < _rows.Count; i++)
            {
                var shown = i < rows.Count;
                _rows[i].Show(shown ? rows[i] : default, shown, _theme);
                if (shown)
                    ShownRows++;
            }
            _found.text = PartySearchTexts.Found(rows.Count);
            _status.text = rows.Count == 0 ? PartySearchTexts.Empty : string.Empty;
            Arrange();
        }

        public void ShowStatus(string text)
        {
            ShownRows = 0;
            foreach (var row in _rows)
                row.Show(default, false, _theme);
            _found.text = PartySearchTexts.Found(0);
            _status.text = text;
            Arrange();
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown)
                return false;
            if (_quick.Contains(worldPoint))
            {
                QuickRequested?.Invoke();
                return true;
            }
            if (_refresh.Contains(worldPoint))
            {
                RefreshRequested?.Invoke();
                return true;
            }
            for (var i = 0; i < _filters.Length; i++)
            {
                if (!_filters[i].Contains(worldPoint) || _searchFilters == null)
                    continue;
                if (i == 0)
                    _searchFilters.NextLanguage();
                else if (i == 1)
                    _searchFilters.NextSlot();
                else
                    _searchFilters.NextWanted();
                ShowFilters();
                RefreshRequested?.Invoke();
                return true;
            }
            foreach (var row in _rows)
            {
                if (!row.JoinContains(worldPoint))
                    continue;
                JoinRequested?.Invoke(row.PartyId);
                return true;
            }
            return false;
        }

        public Vector2 WorldPointOfQuick() => _quick.Rect.TransformPoint(_quick.Rect.rect.center);

        public Vector2 WorldPointOfFilter(int index) => _filters[index].Rect.TransformPoint(_filters[index].Rect.rect.center);

        public Vector2 WorldPointOfJoin(int row) => _rows[row].JoinPoint;

        private void ShowFilters()
        {
            if (_searchFilters == null)
                return;
            _filters[0].Text = _searchFilters.LanguageLabel;
            _filters[1].Text = _searchFilters.SlotLabel;
            _filters[2].Text = _searchFilters.WantedLabel;
        }

        private void Arrange()
        {
            var width = Width();
            var y = TopPx;
            Place((RectTransform)_quick.Rect.parent, y, QuickCardPx);
            y += QuickCardPx + GapPx;
            var filterWidth = (width - 2f * GapPx) / _filters.Length;
            for (var i = 0; i < _filters.Length; i++)
                UiElements.Anchor(_filters[i].Rect, new Vector2(0f, 1f), new Vector2(PaddingPx + i * (filterWidth + GapPx), -y), new Vector2(filterWidth, FilterPx));
            y += FilterPx + GapPx;
            UiElements.Anchor(_found.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(width - RefreshWidthPx - GapPx, ListHeaderPx));
            UiElements.Anchor(_refresh.Rect, new Vector2(1f, 1f), new Vector2(-PaddingPx, -y), new Vector2(RefreshWidthPx, ListHeaderPx));
            y += ListHeaderPx + GapPx;
            var listTop = y;
            for (var i = 0; i < _rows.Count; i++)
            {
                var fits = y + RowPx <= _contentPx;
                _rows[i].Fit(fits);
                if (!fits)
                    continue;
                Place(_rows[i].Rect, y, RowPx);
                y += RowPx + GapPx;
            }
            Place(_status.rectTransform, listTop, StatusPx);
        }

        private UiButton BuildQuickCard()
        {
            var card = UiElements.Fill("Quick", _root, _theme.Card, RadiusPx).rectTransform;
            var textWidth = Width() - QuickButtonWidthPx - 3f * PaddingPx;
            var title = UiElements.Label("Title", card, _theme.BoldFont, 14f, _theme.Text, TextAlignmentOptions.BottomLeft);
            UiElements.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -6f), new Vector2(textWidth, 18f));
            title.text = PartySearchTexts.Quick;
            var hint = UiElements.Label("Hint", card, _theme.TextFont, 10f, _theme.TextDim, TextAlignmentOptions.TopLeft);
            UiElements.Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -26f), new Vector2(textWidth, 26f));
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            hint.text = PartySearchTexts.QuickHint;
            var button = Button(card, PartyActions.Label(PartyAction.QuickMatch, string.Empty), _theme.Gold, _theme.TextOnGold, _theme.Gold, 12f);
            UiElements.Anchor(button.Rect, new Vector2(1f, 0.5f), new Vector2(-PaddingPx, 0f), new Vector2(QuickButtonWidthPx, ButtonPx));
            return button;
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

        private sealed class ResultRow
        {
            private readonly Image _back;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _details;
            private readonly TextMeshProUGUI _tags;
            private readonly TextMeshProUGUI _seats;
            private readonly UiButton _join;
            private bool _shown;
            private bool _fits = true;

            private ResultRow(Image back, TextMeshProUGUI title, TextMeshProUGUI details, TextMeshProUGUI tags, TextMeshProUGUI seats, UiButton join)
            {
                _back = back;
                _title = title;
                _details = details;
                _tags = tags;
                _seats = seats;
                _join = join;
                PartyId = string.Empty;
            }

            public RectTransform Rect => _back.rectTransform;
            public string PartyId { get; private set; }
            public Vector2 JoinPoint => _join.Rect.TransformPoint(_join.Rect.rect.center);

            public static ResultRow Create(int index, RectTransform parent, PartySearchPanel panel)
            {
                var theme = panel._theme;
                var back = UiElements.Fill("Result " + index, parent, theme.Card, RadiusPx);
                var rect = back.rectTransform;
                var textWidth = Width() - JoinWidthPx - 3f * PaddingPx;
                var title = UiElements.Label("Title", rect, theme.BoldFont, 14f, theme.Text, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -5f), new Vector2(textWidth - SeatsWidthPx, 18f));
                title.overflowMode = TextOverflowModes.Ellipsis;
                var seats = UiElements.Label("Seats", rect, theme.BoldFont, 12f, theme.TextDim, TextAlignmentOptions.MidlineRight);
                UiElements.Anchor(seats.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx + textWidth - SeatsWidthPx, -5f), new Vector2(SeatsWidthPx, 18f));
                var details = UiElements.Label("Details", rect, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(details.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -23f), new Vector2(textWidth, 14f));
                details.overflowMode = TextOverflowModes.Ellipsis;
                var tags = UiElements.Label("Tags", rect, theme.TextFont, 11f, TagText, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(tags.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -38f), new Vector2(textWidth, 14f));
                tags.overflowMode = TextOverflowModes.Ellipsis;
                var join = panel.Button(rect, PartySearchTexts.Join, theme.Accent, Color.white, theme.Accent, 12f);
                UiElements.Anchor(join.Rect, new Vector2(1f, 0.5f), new Vector2(-PaddingPx, 0f), new Vector2(JoinWidthPx, JoinHeightPx));
                back.gameObject.SetActive(false);
                return new ResultRow(back, title, details, tags, seats, join);
            }

            public void Show(PartySearchRow row, bool shown, UiTheme theme)
            {
                _shown = shown;
                _back.gameObject.SetActive(_shown && _fits);
                if (!shown)
                {
                    PartyId = string.Empty;
                    return;
                }
                PartyId = row.PartyId;
                _title.text = row.Title;
                _details.text = row.Details;
                _tags.text = row.Tags;
                _seats.text = row.Seats;
            }

            public void Fit(bool fits)
            {
                _fits = fits;
                _back.gameObject.SetActive(_shown && _fits);
            }

            public bool JoinContains(Vector2 worldPoint) => _shown && _fits && PartyId.Length > 0 && _join.Contains(worldPoint);
        }
    }
}
