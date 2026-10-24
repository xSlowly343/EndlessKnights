using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class ItemsPanel
    {
        public const int Columns = 7;
        public const int Rows = 4;

        private const float PaddingPx = 16f;
        private const float TopPx = 4f;
        private const float GapPx = 6f;
        private const float FilterHeightPx = 26f;
        private const float FilterPaddingPx = 12f;
        private const float CellHeightPx = 54f;
        private const float CellRadiusPx = 8f;
        private const float FramePx = 2f;
        private const float CardHeightPx = 150f;
        private const float CardRadiusPx = 12f;
        private const float IconPx = 40f;
        private const float ButtonHeightPx = 32f;

        private static readonly Color CellFill = new Color32(0x22, 0x30, 0x4A, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<UiButton> _filters = new List<UiButton>();
        private readonly List<ItemCell> _cells = new List<ItemCell>();
        private readonly List<WeaponBase> _shown = new List<WeaponBase>();
        private readonly TextMeshProUGUI _empty;
        private readonly RectTransform _card;
        private RarityFrame _cardFrame = null!;
        private RarityFrame _icon = null!;
        private TextMeshProUGUI _iconLetter = null!;
        private TextMeshProUGUI _name = null!;
        private TextMeshProUGUI _subtitle = null!;
        private TextMeshProUGUI _inHands = null!;
        private TextMeshProUGUI _innate = null!;
        private TextMeshProUGUI _lightUp = null!;
        private TextMeshProUGUI _goOut = null!;
        private UiButton _toRight = null!;
        private UiButton _toLeft = null!;
        private ItemFilter _filter = ItemFilter.All;
        private WeaponBase? _selected;
        private GameLoop? _loop;

        private ItemsPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Items", content));
            BuildFilters();
            BuildGrid();
            _empty = UiElements.Label("Empty", _root, theme.TextFont, 14f, theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(_empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(TopPx + FilterHeightPx + GapPx + CellHeightPx)), new Vector2(300f, 24f));
            _card = BuildCard();
            _root.gameObject.SetActive(false);
        }

        public static ItemsPanel Create(RectTransform content, UiTheme theme) => new ItemsPanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public WeaponBase? Selected => _selected;

        public void Bind(GameLoop loop) => _loop = loop;

        public void Show(bool shown)
        {
            _root.gameObject.SetActive(shown);
            if (shown)
                Refresh();
        }

        public void Refresh()
        {
            if (!IsShown || _loop == null)
                return;
            ShowFilters();
            _shown.Clear();
            _shown.AddRange(ItemTexts.Bag(_loop.State, _filter));
            if (_selected != null && !_shown.Contains(_selected))
                _selected = null;
            for (var i = 0; i < _cells.Count; i++)
                _cells[i].Show(i < _shown.Count ? _shown[i] : null, _shown.Count > i && _shown[i] == _selected, _theme);
            _empty.gameObject.SetActive(_shown.Count == 0);
            _empty.text = ItemTexts.Empty(_filter);
            ShowCard(_loop.State);
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown || _loop == null)
                return false;
            for (var i = 0; i < _filters.Count; i++)
            {
                if (!_filters[i].Contains(worldPoint))
                    continue;
                _filter = ItemTexts.Filters[i];
                Refresh();
                return true;
            }
            if (_selected != null && TryEquip(worldPoint))
                return true;
            for (var i = 0; i < _cells.Count; i++)
            {
                if (!_cells[i].Contains(worldPoint))
                    continue;
                _selected = i < _shown.Count && _shown[i] != _selected ? _shown[i] : null;
                Refresh();
                return true;
            }
            return false;
        }

        public void Select(WeaponBase? weapon)
        {
            _selected = weapon;
            Refresh();
        }

        private bool TryEquip(Vector2 worldPoint)
        {
            var hand = _toRight.Contains(worldPoint) ? WeaponHand.Right : _toLeft.Contains(worldPoint) ? WeaponHand.Left : (WeaponHand?)null;
            if (hand == null)
                return UiElements.Contains(_card, worldPoint);
            _loop!.Weapons.Equip(hand.Value, _selected!);
            _selected = null;
            Refresh();
            return true;
        }

        private void ShowFilters()
        {
            for (var i = 0; i < _filters.Count; i++)
            {
                var active = ItemTexts.Filters[i] == _filter;
                _filters[i].Back.color = active ? _theme.Gold : _theme.Border;
                _filters[i].Rect.GetChild(0).GetComponent<Image>().color = active ? new Color32(0x3A, 0x2E, 0x14, 0xFF) : _theme.Background;
                _filters[i].Label!.color = active ? _theme.Gold : _theme.Text;
            }
        }

        private void ShowCard(GameState state)
        {
            _card.gameObject.SetActive(_selected != null);
            if (_selected == null)
                return;
            var weapon = _selected;
            var rarity = ItemTexts.RarityOf(weapon);
            _cardFrame.Show(rarity, true);
            _icon.Show(rarity, false);
            _iconLetter.text = ItemTexts.Initial(weapon);
            _iconLetter.color = RarityFrame.IconOf(rarity);
            _name.text = ItemTexts.Name(weapon);
            _name.color = RarityFrame.TitleOf(rarity);
            _subtitle.text = ItemTexts.Subtitle(weapon);
            _inHands.text = ItemTexts.InHands(state);
            _innate.text = ItemTexts.Innate(weapon);
            var right = WeaponSwap.For(state, weapon, WeaponHand.Right);
            _lightUp.text = ItemTexts.LightUp(right);
            _goOut.text = ItemTexts.GoOut(right);
        }

        private void BuildFilters()
        {
            var row = UiElements.Anchor(UiElements.Rect("Filters", _root), new Vector2(0f, 1f), new Vector2(PaddingPx, -TopPx), new Vector2(GridWidth(), FilterHeightPx));
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = GapPx;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            foreach (var filter in ItemTexts.Filters)
            {
                var border = UiElements.Fill("Filter", row, _theme.Border, FilterHeightPx / 2f);
                var fit = border.gameObject.AddComponent<HorizontalLayoutGroup>();
                fit.padding = new RectOffset((int)FilterPaddingPx, (int)FilterPaddingPx, 0, 0);
                fit.childControlWidth = true;
                fit.childControlHeight = true;
                fit.childForceExpandWidth = false;
                var size = border.gameObject.AddComponent<LayoutElement>();
                size.preferredHeight = FilterHeightPx;
                var inner = UiElements.Fill("Inner", border.rectTransform, _theme.Background, FilterHeightPx / 2f - 1f);
                UiElements.IgnoreLayout(inner.rectTransform);
                UiElements.Stretch(inner.rectTransform, 1f);
                var label = UiElements.Label("Text", border.rectTransform, _theme.TextFont, 12f, _theme.Text, TextAlignmentOptions.Center);
                label.text = ItemTexts.Filter(filter);
                _filters.Add(new UiButton(border, label));
            }
        }

        private void BuildGrid()
        {
            var cellWidth = (GridWidth() - (Columns - 1) * GapPx) / Columns;
            var top = TopPx + FilterHeightPx + GapPx + 2f;
            for (var i = 0; i < Columns * Rows; i++)
            {
                var position = new Vector2(PaddingPx + i % Columns * (cellWidth + GapPx), -(top + i / Columns * (CellHeightPx + GapPx)));
                _cells.Add(ItemCell.Create(_root, position, new Vector2(cellWidth, CellHeightPx), _theme));
            }
        }

        private RectTransform BuildCard()
        {
            var card = UiElements.Anchor(UiElements.Rect("Card", _root), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(GridWidth(), CardHeightPx));
            _cardFrame = RarityFrame.Create(card, CardRadiusPx, 1f, _theme.Card);
            var topLeft = new Vector2(0f, 1f);
            var icon = UiElements.Anchor(UiElements.Rect("Icon", card), topLeft, new Vector2(12f, -10f), Vector2.one * IconPx);
            _icon = RarityFrame.Create(icon, CellRadiusPx, FramePx, _theme.Background);
            _iconLetter = UiElements.Label("Letter", icon, _theme.BoldFont, 20f, _theme.RaritySimple, TextAlignmentOptions.Center);
            UiElements.Stretch(_iconLetter.rectTransform);
            _name = CardText("Name", card, _theme.BoldFont, 15f, _theme.Text, new Vector2(62f, -10f), new Vector2(250f, 20f));
            _subtitle = CardText("Subtitle", card, _theme.TextFont, 12f, _theme.TextDim, new Vector2(62f, -30f), new Vector2(250f, 18f));
            _inHands = UiElements.Label("InHands", card, _theme.TextFont, 11f, _theme.TextDim, TextAlignmentOptions.TopRight);
            UiElements.Anchor(_inHands.rectTransform, Vector2.one, new Vector2(-12f, -10f), new Vector2(130f, 32f));
            _inHands.textWrappingMode = TextWrappingModes.Normal;
            _innate = CardText("Innate", card, _theme.TextFont, 12f, new Color32(0xFF, 0xE2, 0xA0, 0xFF), new Vector2(12f, -58f), new Vector2(GridWidth() - 24f, 16f));
            _lightUp = CardText("LightUp", card, _theme.TextFont, 11f, new Color32(0x6F, 0xE3, 0x9A, 0xFF), new Vector2(12f, -76f), new Vector2(GridWidth() - 24f, 15f));
            _goOut = CardText("GoOut", card, _theme.TextFont, 11f, new Color32(0xFF, 0x8A, 0x80, 0xFF), new Vector2(12f, -91f), new Vector2(GridWidth() - 24f, 15f));
            var buttonWidth = (GridWidth() - 24f - GapPx) / 2f;
            _toRight = GoldButton(card, ItemTexts.EquipTo(WeaponHand.Right), new Vector2(12f, 10f), buttonWidth);
            _toLeft = GoldButton(card, ItemTexts.EquipTo(WeaponHand.Left), new Vector2(12f + buttonWidth + GapPx, 10f), buttonWidth);
            card.gameObject.SetActive(false);
            return card;
        }

        private static TextMeshProUGUI CardText(string name, RectTransform card, TMP_FontAsset? font, float size, Color color, Vector2 position, Vector2 box)
        {
            var label = UiElements.Label(name, card, font, size, color, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(label.rectTransform, new Vector2(0f, 1f), position, box);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private UiButton GoldButton(RectTransform card, string text, Vector2 position, float width)
        {
            var shade = UiElements.Fill(text, card, new Color32(0xB4, 0x7E, 0x1A, 0xFF), CellRadiusPx);
            UiElements.Anchor(shade.rectTransform, Vector2.zero, position, new Vector2(width, ButtonHeightPx));
            var face = UiElements.Fill("Face", shade.rectTransform, _theme.Gold, CellRadiusPx);
            UiElements.Stretch(face.rectTransform).offsetMin = new Vector2(0f, 3f);
            var label = UiElements.Label("Text", shade.rectTransform, _theme.BoldFont, 12f, new Color32(0x2A, 0x1C, 0x00, 0xFF), TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform).offsetMin = new Vector2(0f, 3f);
            label.text = text;
            return new UiButton(shade, label);
        }

        private static float GridWidth() => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;

        private sealed class ItemCell
        {
            private readonly Image _selection;
            private readonly RarityFrame _frame;
            private readonly Image _gem;
            private readonly TextMeshProUGUI _letter;

            private ItemCell(Image selection, RarityFrame frame, Image gem, TextMeshProUGUI letter)
            {
                _selection = selection;
                _frame = frame;
                _gem = gem;
                _letter = letter;
            }

            public static ItemCell Create(RectTransform parent, Vector2 position, Vector2 size, UiTheme theme)
            {
                var selection = UiElements.Fill("Cell", parent, Color.white, CellRadiusPx + 2f);
                UiElements.Anchor(selection.rectTransform, new Vector2(0f, 1f), position, size);
                var host = UiElements.Stretch(UiElements.Rect("Frame", selection.rectTransform), 2f);
                var frame = RarityFrame.Create(host, CellRadiusPx, FramePx, CellFill);
                var letter = UiElements.Label("Letter", host, theme.BoldFont, 20f, theme.RaritySimple, TextAlignmentOptions.Center);
                UiElements.Stretch(letter.rectTransform);
                var gem = UiElements.Dot("Gem", host, theme.Text);
                UiElements.Anchor(gem.rectTransform, Vector2.zero, new Vector2(5f, 5f), Vector2.one * 7f);
                return new ItemCell(selection, frame, gem, letter);
            }

            public bool Contains(Vector2 worldPoint) => UiElements.Contains(_selection.rectTransform, worldPoint);

            public void Show(WeaponBase? weapon, bool selected, UiTheme theme)
            {
                _selection.color = selected ? Color.white : Color.clear;
                var filled = weapon != null;
                _letter.gameObject.SetActive(filled);
                _gem.gameObject.SetActive(filled);
                if (!filled)
                {
                    _frame.ShowEmpty(new Color(theme.Border.r, theme.Border.g, theme.Border.b, 0.45f));
                    return;
                }
                var rarity = ItemTexts.RarityOf(weapon!);
                _frame.Show(rarity, !selected);
                _letter.text = ItemTexts.Initial(weapon!);
                _letter.color = RarityFrame.IconOf(rarity);
                _gem.color = Enum.TryParse<GemColor>(weapon!.Type.Color, true, out var color) ? GemColors.Of(color) : GemColors.White;
            }
        }
    }
}
