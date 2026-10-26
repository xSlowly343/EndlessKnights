using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class HeroPanel
    {
        public const float CompactBelowPx = 400f;

        private const float PaddingPx = 15f;
        private const float GapPx = 8f;
        private const float SlotPx = 56f;
        private const float RingPx = 52f;
        private const float SlotIconShare = 0.55f;
        private const float FramePx = 2f;
        private const float SlotRadiusPx = 8f;
        private const float CenterWidthPx = 212f;
        private const float DollHeightPx = 248f;
        private const float HandHeightPx = 60f;
        private const float JewelryWidthPx = 64f;
        private const float JewelryTitlePx = 12f;
        private const float PortraitWidthPx = 72f;
        private const float PortraitHeightPx = 84f;
        private const float TileHeightPx = 58f;
        private const float HintHeightPx = 34f;
        private const float RowHeightPx = 52f;
        private const float ColumnWidthPx = 150f;
        private const int Columns = 3;

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly RectTransform _overview;
        private readonly RectTransform _doll;
        private readonly RectTransform _tiles;
        private readonly RectTransform _input;
        private readonly List<TextMeshProUGUI> _tileLabels = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _tileValues = new List<TextMeshProUGUI>();
        private readonly HandCard[] _hands = new HandCard[2];
        private readonly List<TextMeshProUGUI[]> _inputRows = new List<TextMeshProUGUI[]>();
        private TextMeshProUGUI _attributes = null!;
        private TextMeshProUGUI _initial = null!;
        private TextMeshProUGUI _title = null!;
        private TextMeshProUGUI _progress = null!;
        private RectTransform _hint = null!;
        private TextMeshProUGUI _hintText = null!;
        private UiButton _toTree = null!;
        private TextMeshProUGUI _sessionTitle = null!;
        private TextMeshProUGUI _allTimeTitle = null!;
        private int _subTab;

        private HeroPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Hero", content));
            _overview = UiElements.Stretch(UiElements.Rect("Overview", _root));
            _doll = BuildDoll();
            _tiles = BuildTiles();
            BuildHint();
            _input = BuildInput();
        }

        public static HeroPanel Create(RectTransform content, UiTheme theme) => new HeroPanel(content, theme);

        public static bool Covers(int subTab) => subTab != 1;

        public bool IsShown => _root.gameObject.activeSelf;

        public event Action? TreeRequested;

        public void Show(bool shown, int subTab, float contentHeightPx)
        {
            _root.gameObject.SetActive(shown);
            if (!shown)
                return;
            _subTab = subTab;
            _overview.gameObject.SetActive(subTab == 0);
            _input.gameObject.SetActive(subTab == 2);
            var compact = contentHeightPx < CompactBelowPx;
            _doll.gameObject.SetActive(!compact);
            _tiles.anchoredPosition = new Vector2(0f, compact ? -GapPx : -(GapPx / 2f + DollHeightPx + 2f * GapPx));
        }

        public void Refresh(GameLoop loop)
        {
            if (!IsShown)
                return;
            if (_subTab == 2)
                RefreshInput(loop);
            else
                RefreshOverview(loop);
        }

        public string InputCell(int row, int column) => _inputRows[row][column].text;

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown || !_toTree.Contains(worldPoint))
                return false;
            TreeRequested?.Invoke();
            return true;
        }

        private void RefreshOverview(GameLoop loop)
        {
            var state = loop.State;
            _attributes.text = HeroSummary.Attributes(loop);
            var name = HeroSummary.ClassName(state.Class);
            _initial.text = name.Length > 0 ? name.Substring(0, 1) : string.Empty;
            _title.text = HeroSummary.Title(state);
            _progress.text = HeroSummary.Progress(state);
            _hands[0].Show(HeroSummary.Hand(loop, WeaponHand.Right), _theme);
            _hands[1].Show(HeroSummary.Hand(loop, WeaponHand.Left), _theme);
            var tiles = HeroSummary.Tiles(loop, NumberDisplay.Settings);
            for (var i = 0; i < _tileValues.Count && i < tiles.Count; i++)
            {
                _tileLabels[i].text = tiles[i].Label;
                _tileValues[i].text = tiles[i].Value;
                _tileValues[i].color = AccentColor(tiles[i].Accent);
            }
            var hint = HeroSummary.Hint(state);
            _hint.gameObject.SetActive(hint != null);
            if (hint != null)
                _hintText.text = hint;
        }

        private void RefreshInput(GameLoop loop)
        {
            _sessionTitle.text = InputStatsView.SessionTitle;
            _allTimeTitle.text = InputStatsView.AllTimeTitle;
            var rows = InputStatsView.Rows(loop.SessionInput, loop.State.TotalInput, NumberDisplay.Settings.ThousandsSeparator);
            for (var i = 0; i < _inputRows.Count && i < rows.Count; i++)
            {
                var cells = _inputRows[i];
                cells[0].text = rows[i].Label;
                cells[1].text = rows[i].HasHint ? $"{rows[i].Session}\n<size=60%><color=#9AA6C8>{rows[i].SessionHint}</color></size>" : rows[i].Session;
                cells[2].text = rows[i].HasHint ? $"{rows[i].AllTime}\n<size=60%><color=#9AA6C8>{rows[i].AllTimeHint}</color></size>" : rows[i].AllTime;
            }
        }

        private Color AccentColor(HeroStatAccent accent) => accent switch
        {
            HeroStatAccent.Gold => _theme.Gold,
            HeroStatAccent.Focus => _theme.FocusTint,
            HeroStatAccent.Resource => _theme.ResourceTint,
            _ => _theme.Text,
        };

        private RectTransform BuildDoll()
        {
            var top = new Vector2(0.5f, 1f);
            var doll = UiElements.Anchor(UiElements.Rect("Doll", _overview), top, new Vector2(0f, -GapPx / 2f), new Vector2(SlotPx + CenterWidthPx + JewelryWidthPx + 4f * GapPx / 2f + GapPx, DollHeightPx));
            var topLeft = new Vector2(0f, 1f);
            for (var i = 0; i < HeroSummary.ArmorSlots.Count; i++)
                SquareSlot(doll, HeroSummary.ArmorSlots[i], new Vector2(0f, -i * (SlotPx + GapPx)));
            var centerX = SlotPx + GapPx + 2f;
            BuildPortrait(doll, centerX);
            var handWidth = (CenterWidthPx - GapPx) / 2f;
            for (var i = 0; i < _hands.Length; i++)
                _hands[i] = HandCard.Create(doll, new Vector2(centerX + i * (handWidth + GapPx), -(DollHeightPx - HandHeightPx)), new Vector2(handWidth, HandHeightPx), _theme);
            var jewelryX = centerX + CenterWidthPx + GapPx + 2f;
            var title = UiElements.Label("JewelryTitle", doll, _theme.TextFont, 10f, _theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(title.rectTransform, topLeft, new Vector2(jewelryX, 0f), new Vector2(JewelryWidthPx, JewelryTitlePx));
            title.text = HeroSummary.JewelryTitle;
            var boxHeight = HeroSummary.JewelrySlots.Count * (RingPx + GapPx) + GapPx / 2f;
            var box = UiElements.Fill("JewelryBox", doll, _theme.Border, 10f);
            UiElements.Anchor(box.rectTransform, topLeft, new Vector2(jewelryX, -(JewelryTitlePx + GapPx)), new Vector2(JewelryWidthPx, boxHeight));
            var inner = UiElements.Fill("Inner", box.rectTransform, _theme.Background, 9f);
            UiElements.Stretch(inner.rectTransform, 1f);
            for (var i = 0; i < HeroSummary.JewelrySlots.Count; i++)
                RingSlot(box.rectTransform, HeroSummary.JewelrySlots[i], new Vector2(0f, -(GapPx / 2f + 2f + i * (RingPx + GapPx))));
            return doll;
        }

        private void BuildPortrait(RectTransform doll, float x)
        {
            var card = UiElements.Fill("Portrait", doll, _theme.Card, 10f);
            UiElements.Anchor(card.rectTransform, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(CenterWidthPx, DollHeightPx - HandHeightPx - GapPx));
            var top = new Vector2(0.5f, 1f);
            _attributes = UiElements.Label("Attributes", card.rectTransform, _theme.TextFont, 12f, _theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(_attributes.rectTransform, top, new Vector2(0f, -GapPx), new Vector2(CenterWidthPx - 2f * GapPx, 16f));
            var figure = UiElements.Fill("Figure", card.rectTransform, _theme.Accent, 24f);
            UiElements.Anchor(figure.rectTransform, top, new Vector2(0f, -30f), new Vector2(PortraitWidthPx, PortraitHeightPx));
            _initial = UiElements.Label("Initial", figure.rectTransform, _theme.BoldFont, 34f, Color.white, TextAlignmentOptions.Center);
            UiElements.Stretch(_initial.rectTransform);
            var bottom = new Vector2(0.5f, 0f);
            _title = UiElements.Label("Title", card.rectTransform, _theme.BoldFont, 15f, _theme.Text, TextAlignmentOptions.Center);
            UiElements.Anchor(_title.rectTransform, bottom, new Vector2(0f, 24f), new Vector2(CenterWidthPx - 2f * GapPx, 20f));
            _progress = UiElements.Label("Progress", card.rectTransform, _theme.TextFont, 12f, _theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(_progress.rectTransform, bottom, new Vector2(0f, GapPx), new Vector2(CenterWidthPx - 2f * GapPx, 16f));
        }

        private void SquareSlot(RectTransform parent, string slot, Vector2 position)
        {
            var frame = UiElements.Fill(slot, parent, _theme.Border, SlotRadiusPx);
            UiElements.Anchor(frame.rectTransform, new Vector2(0f, 1f), position, Vector2.one * SlotPx);
            var inner = UiElements.Fill("Inner", frame.rectTransform, _theme.Card, SlotRadiusPx - FramePx);
            UiElements.Stretch(inner.rectTransform, FramePx);
            SlotMark(frame.rectTransform, slot, SlotPx, 11f);
        }

        private void RingSlot(RectTransform parent, string slot, Vector2 position)
        {
            var frame = UiElements.Dot(slot, parent, _theme.Border);
            UiElements.Anchor(frame.rectTransform, new Vector2(0.5f, 1f), position, Vector2.one * RingPx);
            var inner = UiElements.Dot("Inner", frame.rectTransform, _theme.Card);
            UiElements.Stretch(inner.rectTransform, FramePx);
            SlotMark(frame.rectTransform, slot, RingPx, 10f);
        }

        private void SlotMark(RectTransform parent, string slot, float slotPx, float textPx)
        {
            var sprite = UiIcons.Slot(slot);
            if (sprite != null)
            {
                var icon = UiElements.Icon("Icon", parent, sprite, _theme.TextDim);
                UiElements.Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (slotPx * SlotIconShare));
                return;
            }
            var label = UiElements.Label("Label", parent, _theme.TextFont, textPx, _theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform);
            label.text = HeroSummary.SlotName(slot);
        }

        private RectTransform BuildTiles()
        {
            var tiles = UiElements.Anchor(UiElements.Rect("Tiles", _overview), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(StripWidth(), 2f * TileHeightPx + GapPx));
            var tileWidth = (StripWidth() - (Columns - 1) * GapPx) / Columns;
            for (var i = 0; i < 2 * Columns; i++)
            {
                var position = new Vector2(i % Columns * (tileWidth + GapPx), -(i / Columns) * (TileHeightPx + GapPx));
                var tile = UiElements.Fill("Tile " + i, tiles, _theme.Card, SlotRadiusPx);
                UiElements.Anchor(tile.rectTransform, new Vector2(0f, 1f), position, new Vector2(tileWidth, TileHeightPx));
                var label = UiElements.Label("Label", tile.rectTransform, _theme.TextFont, 11f, _theme.TextDim, TextAlignmentOptions.TopLeft);
                UiElements.Stretch(label.rectTransform, GapPx);
                var value = UiElements.Label("Value", tile.rectTransform, _theme.NumberFont, 19f, _theme.Text, TextAlignmentOptions.BottomLeft);
                UiElements.Stretch(value.rectTransform, GapPx);
                value.rectTransform.offsetMin = new Vector2(10f, 6f);
                label.rectTransform.offsetMin = new Vector2(10f, GapPx);
                _tileLabels.Add(label);
                _tileValues.Add(value);
            }
            return tiles;
        }

        private void BuildHint()
        {
            var back = UiElements.Fill("Hint", _overview, _theme.Card, SlotRadiusPx);
            _hint = UiElements.Anchor(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(StripWidth(), HintHeightPx));
            var dot = UiElements.Dot("Dot", _hint, _theme.Alert);
            UiElements.Anchor(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), Vector2.one * 8f);
            _hintText = UiElements.Label("Text", _hint, _theme.TextFont, 12f, _theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(_hintText.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(StripWidth() - 140f, HintHeightPx));
            var action = UiElements.Fill("ToTree", _hint, Color.clear);
            UiElements.Anchor(action.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, HintHeightPx));
            var label = UiElements.Label("Text", action.rectTransform, _theme.BoldFont, 12f, _theme.Gold, TextAlignmentOptions.MidlineRight);
            UiElements.Stretch(label.rectTransform).offsetMax = new Vector2(-10f, 0f);
            label.text = HeroSummary.TreeAction;
            _toTree = new UiButton(action, label);
            _hint.gameObject.SetActive(false);
        }

        private RectTransform BuildInput()
        {
            var input = UiElements.Stretch(UiElements.Rect("Input", _root));
            var topLeft = new Vector2(0f, 1f);
            var labelWidth = StripWidth() - 2f * ColumnWidthPx;
            _sessionTitle = HeaderCell(input, new Vector2(PaddingPx + labelWidth, -GapPx));
            _allTimeTitle = HeaderCell(input, new Vector2(PaddingPx + labelWidth + ColumnWidthPx, -GapPx));
            for (var i = 0; i < 3; i++)
            {
                var row = UiElements.Fill("Row " + i, input, _theme.Card, SlotRadiusPx);
                UiElements.Anchor(row.rectTransform, topLeft, new Vector2(PaddingPx, -(32f + i * (RowHeightPx + GapPx))), new Vector2(StripWidth(), RowHeightPx));
                var label = UiElements.Label("Label", row.rectTransform, _theme.TextFont, 13f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(labelWidth - 12f, RowHeightPx));
                var session = InputValue(row.rectTransform, labelWidth);
                var allTime = InputValue(row.rectTransform, labelWidth + ColumnWidthPx);
                _inputRows.Add(new[] { label, session, allTime });
            }
            input.gameObject.SetActive(false);
            return input;
        }

        private TextMeshProUGUI HeaderCell(RectTransform parent, Vector2 position)
        {
            var cell = UiElements.Label("Column", parent, _theme.TextFont, 11f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(cell.rectTransform, new Vector2(0f, 1f), position, new Vector2(ColumnWidthPx, 18f));
            return cell;
        }

        private TextMeshProUGUI InputValue(RectTransform row, float x)
        {
            var value = UiElements.Label("Value", row, _theme.NumberFont, 17f, _theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(ColumnWidthPx, RowHeightPx));
            value.lineSpacing = -10f;
            return value;
        }

        private static float StripWidth() => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;

        private sealed class HandCard
        {
            private readonly Image _frame;
            private readonly Image _gem;
            private readonly TextMeshProUGUI _label;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _rarity;

            private HandCard(Image frame, Image gem, TextMeshProUGUI label, TextMeshProUGUI name, TextMeshProUGUI rarity)
            {
                _frame = frame;
                _gem = gem;
                _label = label;
                _name = name;
                _rarity = rarity;
            }

            public static HandCard Create(RectTransform parent, Vector2 position, Vector2 size, UiTheme theme)
            {
                var frame = UiElements.Fill("Hand", parent, theme.Border, SlotRadiusPx);
                UiElements.Anchor(frame.rectTransform, new Vector2(0f, 1f), position, size);
                var inner = UiElements.Fill("Inner", frame.rectTransform, theme.Card, SlotRadiusPx - FramePx);
                UiElements.Stretch(inner.rectTransform, FramePx);
                var topLeft = new Vector2(0f, 1f);
                var label = UiElements.Label("Label", frame.rectTransform, theme.TextFont, 10f, theme.TextDim, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(label.rectTransform, topLeft, new Vector2(10f, -6f), new Vector2(size.x - 16f, 14f));
                var gem = UiElements.Dot("Gem", frame.rectTransform, theme.Text);
                UiElements.Anchor(gem.rectTransform, topLeft, new Vector2(10f, -27f), Vector2.one * 8f);
                var name = UiElements.Label("Name", frame.rectTransform, theme.BoldFont, 12f, theme.Text, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(name.rectTransform, topLeft, new Vector2(23f, -22f), new Vector2(size.x - 28f, 18f));
                name.enableAutoSizing = true;
                name.fontSizeMin = 9f;
                name.fontSizeMax = 12f;
                var rarity = UiElements.Label("Rarity", frame.rectTransform, theme.TextFont, 10f, theme.RaritySimple, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(rarity.rectTransform, topLeft, new Vector2(10f, -41f), new Vector2(size.x - 16f, 14f));
                return new HandCard(frame, gem, label, name, rarity);
            }

            public void Show(HeroHand hand, UiTheme theme)
            {
                _label.text = hand.Label;
                _name.text = hand.Name;
                _name.color = hand.IsEmpty ? theme.TextDim : theme.Text;
                _rarity.text = hand.Rarity;
                _frame.color = hand.IsEmpty ? theme.Border : theme.RaritySimple;
                _gem.gameObject.SetActive(!hand.IsEmpty);
                if (!hand.IsEmpty)
                    _gem.color = GemTint(hand.Color!);
            }

            private static Color GemTint(string color) =>
                Enum.TryParse<GemColor>(color, true, out var gemColor) ? GemColors.Of(gemColor) : GemColors.White;
        }
    }
}
