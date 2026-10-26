using System.Collections.Generic;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public sealed class StatsPanel
    {
        private const float PaddingPx = 15f;
        private const float GapPx = 8f;
        private const float HeaderTopPx = 32f;
        private const float RowHeightPx = 52f;
        private const float ColumnWidthPx = 150f;
        private const float RadiusPx = 8f;

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<TextMeshProUGUI[]> _rows = new List<TextMeshProUGUI[]>();
        private readonly TextMeshProUGUI _sessionTitle;
        private readonly TextMeshProUGUI _allTimeTitle;

        private StatsPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Stats", content));
            var labelWidth = Width() - 2f * ColumnWidthPx;
            _sessionTitle = HeaderCell(new Vector2(PaddingPx + labelWidth, -GapPx));
            _allTimeTitle = HeaderCell(new Vector2(PaddingPx + labelWidth + ColumnWidthPx, -GapPx));
            for (var i = 0; i < InputStatsView.RowCount; i++)
                _rows.Add(BuildRow(i, labelWidth));
            _root.gameObject.SetActive(false);
        }

        public static StatsPanel Create(RectTransform content, UiTheme theme) => new StatsPanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;

        public void Show(bool shown) => _root.gameObject.SetActive(shown);

        public string Cell(int row, int column) => _rows[row][column].text;

        public void Refresh(GameLoop loop)
        {
            if (!IsShown)
                return;
            _sessionTitle.text = InputStatsView.SessionTitle;
            _allTimeTitle.text = InputStatsView.AllTimeTitle;
            var rows = InputStatsView.Rows(loop.SessionInput, loop.State.TotalInput, loop.State.StartedUnixMs, NumberDisplay.Settings.ThousandsSeparator);
            for (var i = 0; i < _rows.Count && i < rows.Count; i++)
            {
                var cells = _rows[i];
                cells[0].text = rows[i].Label;
                cells[1].text = rows[i].Session;
                cells[2].text = rows[i].AllTime;
            }
        }

        private TextMeshProUGUI[] BuildRow(int index, float labelWidth)
        {
            var row = UiElements.Fill("Row " + index, _root, _theme.Card, RadiusPx);
            UiElements.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -(HeaderTopPx + index * (RowHeightPx + GapPx))), new Vector2(Width(), RowHeightPx));
            var label = UiElements.Label("Label", row.rectTransform, _theme.TextFont, 13f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(labelWidth - 12f, RowHeightPx));
            return new[] { label, Value(row.rectTransform, labelWidth), Value(row.rectTransform, labelWidth + ColumnWidthPx) };
        }

        private TextMeshProUGUI HeaderCell(Vector2 position)
        {
            var cell = UiElements.Label("Column", _root, _theme.TextFont, 11f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(cell.rectTransform, new Vector2(0f, 1f), position, new Vector2(ColumnWidthPx, 18f));
            return cell;
        }

        private TextMeshProUGUI Value(RectTransform row, float x)
        {
            var value = UiElements.Label("Value", row, _theme.NumberFont, 17f, _theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(ColumnWidthPx, RowHeightPx));
            value.lineSpacing = -10f;
            return value;
        }

        private static float Width() => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;
    }
}
