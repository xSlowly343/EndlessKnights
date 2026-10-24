using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Battle;
using Game.Presentation.Strip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class BookPanel
    {
        public const int BagColumns = 9;
        public const int BagRows = 2;
        public const float FullBookMinContentPx = 400f;

        private const float PaddingPx = 12f;
        private const float TopPx = 4f;
        private const float GapPx = 6f;
        private const float RowPx = 54f;
        private const float LockedRowPx = 40f;
        private const float RowRadiusPx = 8f;
        private const float RowInsetPx = 10f;
        private const float GemBoxPx = 40f;
        private const float RowGemPx = 26f;
        private const float TriggerWidthPx = 46f;
        private const float TriggerHeightPx = 30f;
        private const float TriggerRadiusPx = 6f;
        private const float DividerHeightPx = 30f;
        private const float BagHeaderPx = 24f;
        private const float FilterPaddingPx = 10f;
        private const float CellPx = 42f;
        private const float BagGemPx = 20f;
        private const float GhostPx = 30f;
        private const float HintPx = 16f;
        private const float ClickSlopPx = 6f;

        private static readonly GemBagFilter[] Filters = { GemBagFilter.All, GemBagFilter.Skills, GemBagFilter.Supports, GemBagFilter.Triggers };
        private static readonly Color CellFill = new Color32(0x22, 0x30, 0x4A, 0xFF);
        private static readonly Color ActivePill = new Color32(0x2E, 0x3A, 0x58, 0xFF);
        private static readonly Color TriggerFill = new Color32(0x4A, 0x3A, 0x16, 0xFF);
        private static readonly Color TriggerText = new Color32(0xFF, 0xE2, 0xA0, 0xFF);
        private static readonly Color LockedBorder = new Color32(0x4A, 0x56, 0x77, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<BagCell> _bag = new List<BagCell>();
        private readonly List<UiButton> _filters = new List<UiButton>();
        private readonly List<GemDef> _bagGems = new List<GemDef>();
        private readonly RectTransform _bagHeader;
        private readonly RectTransform _bagArea;
        private readonly TextMeshProUGUI _hint;
        private readonly GemIcon _ghost;
        private GameLoop? _loop;
        private GemBagFilter _filter = GemBagFilter.All;
        private BookPlace _pressed;
        private Vector2 _pressPoint;
        private bool _dragged;
        private bool _compact;
        private float _contentPx = PanelGeometry.FullHeightPx;

        private BookPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Book", content));
            _bagHeader = BuildBagHeader();
            _bagArea = BuildBag();
            _hint = UiElements.Label("Hint", _root, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.MidlineLeft);
            _hint.overflowMode = TextOverflowModes.Ellipsis;
            _ghost = GemIcon.Create("Ghost", _root, theme);
            _ghost.Hide();
            _root.gameObject.SetActive(false);
        }

        public static BookPanel Create(RectTransform content, UiTheme theme) => new BookPanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public bool IsCompact => _compact;
        public bool IsDragging => _pressed.Spot != BookSpot.None;
        public string Hint => _hint.text;

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            for (var i = _rows.Count; i < loop.Book.MaxSlots; i++)
                _rows.Add(Row.Create(i, _root, _theme));
            _ghost.Rect.SetAsLastSibling();
            ShowHint(BookTexts.Hint);
            Refresh();
        }

        public void Show(bool shown, float contentPx)
        {
            if (!shown)
                CancelPress();
            _root.gameObject.SetActive(shown);
            _contentPx = contentPx;
            if (shown)
                Refresh();
        }

        public void Refresh()
        {
            if (!IsShown || _loop == null || IsDragging)
                return;
            var state = _loop.State;
            var book = _loop.Book;
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i >= book.SlotCount)
                    _rows[i].ShowLocked(book.UnlockStageOf(i), _theme);
                else if (i < state.Skills.Count)
                    _rows[i].Show(state.Skills[i], _loop.Skills.Armed(i), _theme);
                else
                    _rows[i].ShowEmpty(_theme);
            }
            for (var i = 0; i < _filters.Count; i++)
            {
                var active = Filters[i] == _filter;
                _filters[i].Back.color = active ? ActivePill : Color.clear;
                _filters[i].Label!.color = active ? _theme.Text : _theme.TextDim;
            }
            GemBag.Fill(book, state, _filter, _bagGems);
            for (var i = 0; i < _bag.Count; i++)
                _bag[i].Show(i < _bagGems.Count ? _bagGems[i] : null, _theme);
            Arrange();
        }

        public bool TryPress(Vector2 worldPoint)
        {
            if (!IsShown || _loop == null)
                return false;
            for (var i = 0; i < _filters.Count; i++)
            {
                if (!_filters[i].Contains(worldPoint))
                    continue;
                _filter = Filters[i];
                Refresh();
                return true;
            }
            _pressed = PlaceAt(worldPoint, true);
            if (_pressed.Spot == BookSpot.None)
                return false;
            _pressPoint = worldPoint;
            _dragged = false;
            return true;
        }

        public void Drag(Vector2 worldPoint)
        {
            if (!IsDragging)
                return;
            if (!_dragged && (worldPoint - _pressPoint).magnitude * StripSpace.PixelsPerUnit < ClickSlopPx)
                return;
            if (!_dragged)
            {
                _dragged = true;
                _ghost.Show(GemOf(_pressed), GhostPx, _theme);
            }
            _ghost.MoveTo(worldPoint);
        }

        public void Release(Vector2 worldPoint)
        {
            var from = _pressed;
            _pressed = BookPlace.Nowhere;
            _ghost.Hide();
            if (from.Spot == BookSpot.None || _loop == null)
                return;
            if (!_dragged)
            {
                Describe(GemOf(from));
                return;
            }
            var to = PlaceAt(worldPoint, false);
            if (to.Spot == BookSpot.None)
            {
                Refresh();
                return;
            }
            var result = BookDrop.Apply(_loop.Book, _loop.State, from, to);
            Refresh();
            ShowHint(result == BookResult.Done ? BookTexts.Hint : BookTexts.Result(result));
        }

        public void CancelPress()
        {
            _pressed = BookPlace.Nowhere;
            _ghost.Hide();
        }

        public Vector2 WorldPointOfRow(int row) => _rows[row].Card.Rect.TransformPoint(_rows[row].Card.Rect.rect.center);

        public Vector2 WorldPointOfTrigger(int row) => _rows[row].Trigger.Rect.TransformPoint(_rows[row].Trigger.Rect.rect.center);

        public Vector2 WorldPointOfBagCell(int index) => _bag[index].Back.rectTransform.TransformPoint(_bag[index].Back.rectTransform.rect.center);

        private void Arrange()
        {
            _compact = _contentPx < FullBookMinContentPx;
            var width = PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;
            var y = TopPx;
            var natural = 0f;
            foreach (var row in _rows)
                natural += row.NaturalPx + GapPx;
            var room = _contentPx - TopPx - HintPx - GapPx - (_compact ? 0f : BagHeaderPx + GapPx + BagHeightPx() + GapPx);
            var scale = natural > 0f ? Mathf.Min(1f, room / natural) : 1f;
            foreach (var row in _rows)
            {
                var height = row.NaturalPx * scale;
                row.Place(new Vector2(PaddingPx, -y), new Vector2(width, height));
                y += height + GapPx * scale;
            }
            _bagHeader.gameObject.SetActive(!_compact);
            _bagArea.gameObject.SetActive(!_compact);
            if (!_compact)
            {
                UiElements.Anchor(_bagHeader, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(width, BagHeaderPx));
                y += BagHeaderPx + GapPx;
                UiElements.Anchor(_bagArea, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(width, BagHeightPx()));
                y += BagHeightPx() + GapPx;
            }
            UiElements.Anchor(_hint.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(width, HintPx));
            if (_compact && _hint.text == BookTexts.Hint)
                ShowHint(BookTexts.CompactHint);
            else if (!_compact && _hint.text == BookTexts.CompactHint)
                ShowHint(BookTexts.Hint);
        }

        private static float BagHeightPx() => BagRows * CellPx + (BagRows - 1) * GapPx;

        private RectTransform BuildBagHeader()
        {
            var header = UiElements.Rect("BagHeader", _root);
            var title = UiElements.Label("Title", header, _theme.BoldFont, 13f, _theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Stretch(title.rectTransform);
            title.text = BookTexts.BagTitle;
            var pills = UiElements.Anchor(UiElements.Rect("Filters", header), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(320f, BagHeaderPx));
            var line = pills.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = GapPx;
            line.childAlignment = TextAnchor.MiddleRight;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            foreach (var filter in Filters)
            {
                var back = UiElements.Fill("Filter", pills, Color.clear, BagHeaderPx / 2f);
                var fit = back.gameObject.AddComponent<HorizontalLayoutGroup>();
                fit.padding = new RectOffset((int)FilterPaddingPx, (int)FilterPaddingPx, 0, 0);
                fit.childControlWidth = true;
                fit.childControlHeight = true;
                fit.childForceExpandWidth = false;
                back.gameObject.AddComponent<LayoutElement>().preferredHeight = BagHeaderPx;
                var label = UiElements.Label("Text", back.rectTransform, _theme.TextFont, 12f, _theme.TextDim, TextAlignmentOptions.Center);
                label.text = BookTexts.Filter(filter);
                _filters.Add(new UiButton(back, label));
            }
            return header;
        }

        private RectTransform BuildBag()
        {
            var area = UiElements.Rect("Bag", _root);
            var width = PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;
            var cellWidth = (width - (BagColumns - 1) * GapPx) / BagColumns;
            for (var i = 0; i < BagColumns * BagRows; i++)
            {
                var position = new Vector2(i % BagColumns * (cellWidth + GapPx), -(i / BagColumns) * (CellPx + GapPx));
                _bag.Add(BagCell.Create(area, position, new Vector2(cellWidth, CellPx), _theme));
            }
            return area;
        }

        private BookPlace PlaceAt(Vector2 worldPoint, bool needsGem)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.TriggerContains(worldPoint) && (!needsGem || row.TriggerGem != null))
                    return BookPlace.Trigger(i);
                if (row.Contains(worldPoint) && (!needsGem || row.SkillGem != null))
                    return BookPlace.Skill(i);
            }
            if (_compact)
                return BookPlace.Nowhere;
            foreach (var cell in _bag)
            {
                if (cell.Contains(worldPoint) && cell.Gem != null)
                    return BookPlace.Bag(cell.Gem);
            }
            return !needsGem && UiElements.Contains(_bagArea, worldPoint) ? BookPlace.Bag() : BookPlace.Nowhere;
        }

        private GemDef? GemOf(BookPlace place)
        {
            if (_loop == null)
                return null;
            var skills = _loop.State.Skills;
            return place.Spot switch
            {
                BookSpot.Bag => place.Gem,
                BookSpot.Skill when place.Row < skills.Count => skills[place.Row].Skill,
                BookSpot.Trigger when place.Row < skills.Count => skills[place.Row].Trigger,
                _ => null
            };
        }

        private void Describe(GemDef? gem)
        {
            if (gem != null && _loop != null)
                ShowHint(BookTexts.Describe(gem, _loop.Book.FreeCopies(gem)));
        }

        private void ShowHint(string text) => _hint.text = text;

        private sealed class GemIcon
        {
            private const float DiamondRadiusPx = 4f;
            private const float DiamondShare = 0.72f;

            private readonly Image _shape;

            private GemIcon(Image shape) => _shape = shape;

            public RectTransform Rect => _shape.rectTransform;

            public static GemIcon Create(string name, RectTransform parent, UiTheme theme)
            {
                var shape = UiElements.Dot(name, parent, theme.Text);
                var rect = shape.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                return new GemIcon(shape);
            }

            public void Show(GemDef? gem, float sizePx, UiTheme theme)
            {
                _shape.gameObject.SetActive(true);
                var diamond = gem != null && gem.Kind == GemKind.Skill;
                _shape.sprite = gem == null ? UiSprites.Ring : diamond ? UiSprites.Rounded : UiSprites.Circle;
                _shape.type = diamond ? Image.Type.Sliced : Image.Type.Simple;
                _shape.pixelsPerUnitMultiplier = diamond ? UiSprites.MultiplierFor(DiamondRadiusPx) : 1f;
                _shape.color = gem == null ? theme.Border : GemColors.Of(gem.Color);
                Rect.localRotation = Quaternion.Euler(0f, 0f, diamond ? 45f : 0f);
                Rect.sizeDelta = Vector2.one * (diamond ? sizePx * DiamondShare : sizePx);
            }

            public void MoveTo(Vector2 worldPoint) => Rect.position = new Vector3(worldPoint.x, worldPoint.y, Rect.position.z);

            public void Hide() => _shape.gameObject.SetActive(false);
        }

        private sealed class BagCell
        {
            private readonly GemIcon _icon;

            private BagCell(Image back, GemIcon icon)
            {
                Back = back;
                _icon = icon;
            }

            public Image Back { get; }
            public GemDef? Gem { get; private set; }

            public static BagCell Create(RectTransform parent, Vector2 position, Vector2 size, UiTheme theme)
            {
                var back = UiElements.Fill("Cell", parent, CellFill, RowRadiusPx);
                UiElements.Anchor(back.rectTransform, new Vector2(0f, 1f), position, size);
                return new BagCell(back, GemIcon.Create("Gem", back.rectTransform, theme));
            }

            public bool Contains(Vector2 worldPoint) => UiElements.Contains(Back.rectTransform, worldPoint);

            public void Show(GemDef? gem, UiTheme theme)
            {
                Gem = gem;
                _icon.Show(gem, gem != null && gem.Kind == GemKind.Skill ? BagGemPx * 1.25f : BagGemPx, theme);
            }
        }

        private sealed class Row
        {
            private const float TextLeftPx = 2f * RowInsetPx + GemBoxPx;
            private const float TextRightPx = 3f * RowInsetPx + TriggerWidthPx;
            private const float LineGapPx = 2f;

            private readonly int _index;
            private readonly Image _border;
            private readonly GemIcon _gem;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _detail;
            private readonly Image _divider;
            private readonly TextMeshProUGUI _triggerText;
            private readonly Image _triggerInner;
            private bool _locked;

            private Row(int index, UiButton card, Image border, GemIcon gem, TextMeshProUGUI name, TextMeshProUGUI detail, Image divider, UiButton trigger, Image triggerInner, TextMeshProUGUI triggerText)
            {
                _index = index;
                Card = card;
                _border = border;
                _gem = gem;
                _name = name;
                _detail = detail;
                _divider = divider;
                Trigger = trigger;
                _triggerInner = triggerInner;
                _triggerText = triggerText;
            }

            public UiButton Card { get; }
            public UiButton Trigger { get; }
            public GemDef? SkillGem { get; private set; }
            public GemDef? TriggerGem { get; private set; }
            public float NaturalPx => _locked ? LockedRowPx : RowPx;

            public static Row Create(int index, RectTransform parent, UiTheme theme)
            {
                var border = UiElements.Fill("Row " + index, parent, Color.clear, RowRadiusPx);
                var back = UiElements.Fill("Back", border.rectTransform, theme.Card, RowRadiusPx - PanelCanvas.BorderPx);
                UiElements.Stretch(back.rectTransform, PanelCanvas.BorderPx);
                var rect = border.rectTransform;
                var middleLeft = new Vector2(0f, 0.5f);
                var gemBox = UiElements.Anchor(UiElements.Rect("GemBox", rect), middleLeft, new Vector2(RowInsetPx, 0f), Vector2.one * GemBoxPx);
                var gem = GemIcon.Create("Gem", gemBox, theme);
                var textLeft = TextLeftPx;
                var name =UiElements.Label("Name", rect, theme.BoldFont, 14f, theme.Text, TextAlignmentOptions.BottomLeft);
                name.overflowMode = TextOverflowModes.Ellipsis;
                var detail = UiElements.Label("Detail", rect, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.TopLeft);
                detail.overflowMode = TextOverflowModes.Ellipsis;
                Fit(name.rectTransform, textLeft, TextRightPx, 0.5f, 1f, LineGapPx);
                Fit(detail.rectTransform, textLeft, TextRightPx, 0f, 0.5f, -LineGapPx);
                var middleRight = new Vector2(1f, 0.5f);
                var divider = UiElements.Fill("Divider", rect, theme.Border);
                UiElements.Anchor(divider.rectTransform, middleRight, new Vector2(-(2f * RowInsetPx + TriggerWidthPx), 0f), new Vector2(PanelCanvas.BorderPx, DividerHeightPx));
                var triggerBack = UiElements.Fill("Trigger", rect, theme.Gold, TriggerRadiusPx);
                UiElements.Anchor(triggerBack.rectTransform, middleRight, new Vector2(-RowInsetPx, 0f), new Vector2(TriggerWidthPx, TriggerHeightPx));
                var triggerInner = UiElements.Fill("Inner", triggerBack.rectTransform, theme.Card, TriggerRadiusPx - PanelCanvas.BorderPx);
                UiElements.Stretch(triggerInner.rectTransform, PanelCanvas.BorderPx);
                var triggerText = UiElements.Label("Text", triggerBack.rectTransform, theme.TextFont, 11f, theme.Gold, TextAlignmentOptions.Center);
                UiElements.Stretch(triggerText.rectTransform, 2f);
                triggerText.overflowMode = TextOverflowModes.Ellipsis;
                return new Row(index, new UiButton(border, name), border, gem, name, detail, divider, new UiButton(triggerBack, triggerText), triggerInner, triggerText);
            }

            public void Place(Vector2 position, Vector2 size) => UiElements.Anchor(Card.Rect, new Vector2(0f, 1f), position, size);

            public bool Contains(Vector2 worldPoint) => !_locked && Card.Contains(worldPoint) && !Trigger.Contains(worldPoint);

            public bool TriggerContains(Vector2 worldPoint) => !_locked && Trigger.Contains(worldPoint);

            public void Show(SkillSlot slot, bool armed, UiTheme theme)
            {
                Open(theme);
                SkillGem = slot.Skill;
                TriggerGem = slot.Trigger;
                _gem.Show(slot.Skill, RowGemPx / 0.72f, theme);
                _name.text = BookTexts.Name(slot.Skill);
                _name.color = armed ? theme.Text : theme.TextDim;
                _detail.text = armed ? BookTexts.RowKey(_index) : $"{BookTexts.RowKey(_index)} · <color=#{ColorUtility.ToHtmlStringRGB(theme.Alert)}>{BookTexts.Unarmed}</color>";
                ShowTrigger(slot.Trigger, theme);
            }

            public void ShowEmpty(UiTheme theme)
            {
                Open(theme);
                SkillGem = null;
                TriggerGem = null;
                _gem.Show(null, RowGemPx, theme);
                _name.text = BookTexts.EmptyRow;
                _name.color = theme.TextDim;
                _detail.text = BookTexts.RowKey(_index);
                ShowTrigger(null, theme);
            }

            public void ShowLocked(long unlockStage, UiTheme theme)
            {
                _locked = true;
                SkillGem = null;
                TriggerGem = null;
                _border.color = LockedBorder;
                Card.Rect.GetChild(0).GetComponent<Image>().color = theme.Background;
                _gem.Hide();
                _divider.gameObject.SetActive(false);
                Trigger.Show(false);
                _name.text = string.Empty;
                _detail.text = BookTexts.Locked(unlockStage);
                _detail.alignment = TextAlignmentOptions.MidlineLeft;
                _detail.fontSize = 12f;
                Fit(_detail.rectTransform, RowInsetPx + 2f, RowInsetPx, 0f, 1f, 0f);
            }

            private void Open(UiTheme theme)
            {
                _locked = false;
                _border.color = Color.clear;
                Card.Rect.GetChild(0).GetComponent<Image>().color = theme.Card;
                _divider.gameObject.SetActive(true);
                Trigger.Show(true);
                _detail.alignment = TextAlignmentOptions.TopLeft;
                _detail.fontSize = 11f;
                Fit(_detail.rectTransform, TextLeftPx, TextRightPx, 0f, 0.5f, -LineGapPx);
            }

            private void ShowTrigger(GemDef? trigger, UiTheme theme)
            {
                _triggerInner.color = trigger != null ? TriggerFill : theme.Card;
                _triggerText.color = trigger != null ? TriggerText : theme.Gold;
                _triggerText.text = trigger != null ? BookTexts.Name(trigger) : BookTexts.AddTrigger;
            }

            private static void Fit(RectTransform rect, float leftPx, float rightPx, float bottom, float top, float shiftPx)
            {
                rect.anchorMin = new Vector2(0f, bottom);
                rect.anchorMax = new Vector2(1f, top);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.offsetMin = new Vector2(leftPx, shiftPx);
                rect.offsetMax = new Vector2(-rightPx, shiftPx);
            }
        }
    }
}
