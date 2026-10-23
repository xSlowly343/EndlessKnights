using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Battle;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Book
{
    public sealed class BookPanel : MonoBehaviour
    {
        public const int BagColumns = 9;
        public const int BagRows = 2;
        public const float FullBookMinContentPx = 400f;

        private const float MarginPx = 8f;
        private const float GapPx = 4f;
        private const float MaxRowPx = 54f;
        private const float KeyWidthPx = 40f;
        private const float MessagePx = 22f;
        private const float FilterRowPx = 26f;
        private const float FilterWidthPx = 90f;
        private const float MaxCellPx = 44f;
        private const float ClickSlopPx = 6f;
        private const float RefreshSeconds = 1f;

        private static readonly GemBagFilter[] Filters = { GemBagFilter.All, GemBagFilter.Skills, GemBagFilter.Triggers };
        private static readonly Color RowColor = new Color(0.13f, 0.19f, 0.29f, 1f);
        private static readonly Color LockedColor = new Color(0.1f, 0.12f, 0.17f, 1f);
        private static readonly Color EmptyCellColor = new Color(0.08f, 0.1f, 0.16f, 1f);
        private static readonly Color PillColor = new Color(0.18f, 0.23f, 0.35f, 1f);
        private static readonly Color ActivePillColor = new Color(0.94f, 0.71f, 0.24f, 1f);
        private static readonly Color LineColor = new Color(0.92f, 0.94f, 1f, 1f);
        private static readonly Color DimLineColor = new Color(0.55f, 0.6f, 0.7f, 1f);
        private static readonly Color MouseKeyColor = new Color(0.94f, 0.71f, 0.24f, 1f);

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Cell> _bag = new List<Cell>();
        private readonly List<StripButton> _filters = new List<StripButton>();
        private readonly List<TextMesh> _filterTexts = new List<TextMesh>();
        private readonly List<GemDef> _bagGems = new List<GemDef>();

        private GameLoop? _loop;
        private StripButton _bagArea = null!;
        private TextMesh _message = null!;
        private Cell _ghost = null!;
        private GemBagFilter _filter = GemBagFilter.All;
        private BookPlace _pressed;
        private Vector2 _pressPoint;
        private bool _dragged;
        private bool _compact;
        private float _pixelsPerUnit = 1f;
        private float _sinceRefresh;
        private float _ghostSize = 1f;

        public static BookPanel Create(Transform panel)
        {
            var book = panel.gameObject.AddComponent<BookPanel>();
            book.Build();
            return book;
        }

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            for (var i = 0; i < loop.Book.MaxSlots; i++)
                _rows.Add(new Row(i, transform));
            Refresh();
        }

        public void Arrange(float left, float bottom, float width, float height, float pixelsPerUnit)
        {
            _pixelsPerUnit = pixelsPerUnit;
            _compact = height * pixelsPerUnit < FullBookMinContentPx;
            var margin = Units(MarginPx);
            var gap = Units(GapPx);
            var top = bottom + height;
            _ghostSize = Units(MaxCellPx);
            var inner = width - 2f * margin;

            var messageHeight = Units(MessagePx);
            _message.transform.position = new Vector2(left + margin, bottom + margin + messageHeight / 2f);
            var bagTop = bottom + margin + messageHeight + gap;
            if (!_compact)
                bagTop = ArrangeBag(left + margin, bagTop, inner, gap);
            ShowBag(!_compact);
            if (_compact && _message.text == BookTexts.Hint)
                _message.text = BookTexts.CompactHint;
            else if (!_compact && _message.text == BookTexts.CompactHint)
                _message.text = BookTexts.Hint;

            var rowsHeight = top - margin - bagTop - gap;
            var count = Mathf.Max(1, _rows.Count);
            var rowHeight = Mathf.Min(Units(MaxRowPx), (rowsHeight - (count - 1) * gap) / count);
            for (var i = 0; i < _rows.Count; i++)
            {
                var y = top - margin - rowHeight / 2f - i * (rowHeight + gap);
                _rows[i].Arrange(left + margin, y, inner, rowHeight, Units(KeyWidthPx), margin);
            }
            Refresh();
        }

        public bool TryPress(Vector2 worldPoint)
        {
            if (_loop == null)
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
            if (_pressed.Spot == BookSpot.None)
                return;
            if (!_dragged && (worldPoint - _pressPoint).magnitude * _pixelsPerUnit < ClickSlopPx)
                return;
            if (!_dragged)
            {
                _dragged = true;
                _ghost.SetVisible(true);
                _ghost.Show(GemOf(_pressed), _ghostSize);
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
                return;
            var result = BookDrop.Apply(_loop.Book, _loop.State, from, to);
            Refresh();
            _message.text = result == BookResult.Done ? BookTexts.Hint : BookTexts.Result(result);
        }

        public void CancelPress()
        {
            _pressed = BookPlace.Nowhere;
            _ghost.Hide();
        }

        private void Build()
        {
            _bagArea = StripButton.Create("BagArea", EmptyCellColor, Vector2.zero, Vector2.one, transform);
            _bagArea.GetComponent<SpriteRenderer>().sortingOrder = 6;
            for (var i = 0; i < Filters.Length; i++)
            {
                var pill = StripButton.Create("Filter " + i, PillColor, Vector2.zero, Vector2.one, transform);
                pill.GetComponent<SpriteRenderer>().sortingOrder = 7;
                _filters.Add(pill);
                _filterTexts.Add(StripText.Create("FilterText " + i, BookTexts.Filter(Filters[i]), Vector2.zero, 0.13f, TextAnchor.MiddleCenter, LineColor, transform, 9));
            }
            for (var i = 0; i < BagColumns * BagRows; i++)
                _bag.Add(new Cell("Bag " + i, transform, 7));
            _message = StripText.Create("Message", BookTexts.Hint, Vector2.zero, 0.13f, TextAnchor.MiddleLeft, DimLineColor, transform, 9);
            _ghost = new Cell("Ghost", transform, 12);
            _ghost.Hide();
        }

        private float ArrangeBag(float left, float bottom, float width, float gap)
        {
            var cell = Mathf.Min(Units(MaxCellPx), (width - (BagColumns - 1) * gap) / BagColumns);
            var gridWidth = BagColumns * cell + (BagColumns - 1) * gap;
            var gridHeight = BagRows * cell + (BagRows - 1) * gap;
            var gridLeft = left + (width - gridWidth) / 2f;
            Place(_bagArea.transform, new Vector2(left + width / 2f, bottom + gridHeight / 2f), new Vector2(width, gridHeight + gap));
            for (var i = 0; i < _bag.Count; i++)
            {
                var column = i % BagColumns;
                var row = i / BagColumns;
                var centre = new Vector2(gridLeft + cell / 2f + column * (cell + gap), bottom + gridHeight - cell / 2f - row * (cell + gap));
                _bag[i].Arrange(centre, cell);
            }
            var filterY = bottom + gridHeight + gap + Units(FilterRowPx) / 2f;
            var pill = new Vector2(Units(FilterWidthPx), Units(FilterRowPx) - gap);
            for (var i = 0; i < _filters.Count; i++)
            {
                var centre = new Vector2(left + pill.x / 2f + i * (pill.x + gap), filterY);
                Place(_filters[i].transform, centre, pill);
                _filterTexts[i].transform.position = centre;
            }
            return bottom + gridHeight + gap + Units(FilterRowPx);
        }

        private void ShowBag(bool visible)
        {
            _bagArea.gameObject.SetActive(visible);
            for (var i = 0; i < _filters.Count; i++)
            {
                _filters[i].gameObject.SetActive(visible);
                _filterTexts[i].gameObject.SetActive(visible);
            }
            for (var i = 0; i < _bag.Count; i++)
                _bag[i].SetVisible(visible);
        }

        private void LateUpdate()
        {
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh < RefreshSeconds || _pressed.Spot != BookSpot.None)
                return;
            Refresh();
        }

        private void Refresh()
        {
            _sinceRefresh = 0f;
            if (_loop == null)
                return;
            var state = _loop.State;
            var book = _loop.Book;
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i >= book.SlotCount)
                    _rows[i].ShowLocked(book.UnlockStageOf(i));
                else
                    _rows[i].Show(i < state.Skills.Count ? state.Skills[i] : null);
            }
            for (var i = 0; i < _filters.Count; i++)
            {
                var active = Filters[i] == _filter;
                _filters[i].GetComponent<SpriteRenderer>().color = active ? ActivePillColor : PillColor;
                _filterTexts[i].color = active ? RowColor : LineColor;
            }
            GemBag.Fill(book, state, _filter, _bagGems);
            for (var i = 0; i < _bag.Count; i++)
                _bag[i].Show(i < _bagGems.Count ? _bagGems[i] : null);
        }

        private BookPlace PlaceAt(Vector2 worldPoint, bool needsGem)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.TriggerContains(worldPoint) && (!needsGem || row.Trigger != null))
                    return BookPlace.Trigger(i);
                if (row.SkillContains(worldPoint) && (!needsGem || row.Skill != null))
                    return BookPlace.Skill(i);
            }
            if (_compact)
                return BookPlace.Nowhere;
            for (var i = 0; i < _bag.Count; i++)
            {
                if (_bag[i].Contains(worldPoint) && _bag[i].Gem != null)
                    return BookPlace.Bag(_bag[i].Gem);
            }
            return !needsGem && _bagArea.Contains(worldPoint) ? BookPlace.Bag() : BookPlace.Nowhere;
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
            if (gem == null || _loop == null)
                return;
            var copies = _loop.Book.FreeCopies(gem);
            _message.text = copies > 0 ? $"{gem.NameRu} · {BookTexts.Kind(gem.Kind)} · в сумке {copies}" : $"{gem.NameRu} · {BookTexts.Kind(gem.Kind)}";
        }

        private float Units(float px) => px / _pixelsPerUnit;

        private static void Place(Transform item, Vector2 centre, Vector2 size)
        {
            item.position = new Vector3(centre.x, centre.y, item.position.z);
            item.localScale = new Vector3(size.x, size.y, 1f);
        }

        private sealed class Cell
        {
            private readonly StripButton _back;
            private readonly TextMesh _label;

            public Cell(string name, Transform parent, int sortingOrder)
            {
                _back = StripButton.Create(name, EmptyCellColor, Vector2.zero, Vector2.one, parent);
                _back.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
                _label = StripText.Create(name + " Text", string.Empty, Vector2.zero, 0.13f, TextAnchor.MiddleCenter, LineColor, parent, sortingOrder + 2);
            }

            public GemDef? Gem { get; private set; }

            public bool Contains(Vector2 worldPoint) => _back.Contains(worldPoint);

            public void Arrange(Vector2 centre, float size)
            {
                Place(_back.transform, centre, Vector2.one * size);
                _label.transform.position = centre;
            }

            public void Show(GemDef? gem)
            {
                Gem = gem;
                _back.GetComponent<SpriteRenderer>().color = gem == null ? EmptyCellColor : GemColors.Of(gem.Color);
                _label.text = gem == null ? string.Empty : BookTexts.Short(gem.NameRu);
            }

            public void SetVisible(bool visible)
            {
                _back.gameObject.SetActive(visible);
                _label.gameObject.SetActive(visible);
            }

            public void MoveTo(Vector2 centre)
            {
                _back.transform.position = new Vector3(centre.x, centre.y, _back.transform.position.z);
                _label.transform.position = centre;
            }

            public void Show(GemDef? gem, float size)
            {
                Show(gem);
                _back.transform.localScale = new Vector3(size, size, 1f);
            }

            public void Hide() => SetVisible(false);
        }

        private sealed class Row
        {
            private readonly int _index;
            private readonly StripButton _back;
            private readonly TextMesh _key;
            private readonly TextMesh _name;
            private readonly Cell _skill;
            private readonly Cell _trigger;

            public Row(int index, Transform parent)
            {
                _index = index;
                _back = StripButton.Create("Row " + index, RowColor, Vector2.zero, Vector2.one, parent);
                _back.GetComponent<SpriteRenderer>().sortingOrder = 6;
                _key = StripText.Create("RowKey " + index, BookTexts.RowKey(index), Vector2.zero, 0.13f, TextAnchor.MiddleCenter, index < 2 ? MouseKeyColor : DimLineColor, parent, 9);
                _name = StripText.Create("RowName " + index, string.Empty, Vector2.zero, 0.14f, TextAnchor.MiddleLeft, LineColor, parent, 9);
                _skill = new Cell("RowSkill " + index, parent, 7);
                _trigger = new Cell("RowTrigger " + index, parent, 7);
            }

            public GemDef? Skill => _skill.Gem;
            public GemDef? Trigger => _trigger.Gem;

            public bool SkillContains(Vector2 worldPoint) => _back.Contains(worldPoint) && !_trigger.Contains(worldPoint);

            public bool TriggerContains(Vector2 worldPoint) => _trigger.Contains(worldPoint);

            public void Arrange(float left, float y, float width, float height, float keyWidth, float margin)
            {
                Place(_back.transform, new Vector2(left + width / 2f, y), new Vector2(width, height));
                _key.transform.position = new Vector2(left + keyWidth / 2f, y);
                var gem = height - margin;
                _skill.Arrange(new Vector2(left + keyWidth + gem / 2f, y), gem);
                _name.transform.position = new Vector2(left + keyWidth + gem + margin, y);
                _trigger.Arrange(new Vector2(left + width - margin - gem / 2f, y), gem);
            }

            public void Show(SkillSlot? slot)
            {
                _back.GetComponent<SpriteRenderer>().color = RowColor;
                _key.gameObject.SetActive(true);
                _skill.SetVisible(true);
                _trigger.SetVisible(true);
                _skill.Show(slot?.Skill);
                _trigger.Show(slot?.Trigger);
                _name.color = slot == null ? DimLineColor : LineColor;
                _name.text = slot == null ? BookTexts.EmptyRow : slot.Skill.NameRu;
            }

            public void ShowLocked(long unlockStage)
            {
                _back.GetComponent<SpriteRenderer>().color = LockedColor;
                _key.gameObject.SetActive(_index < 2);
                _skill.Show(null);
                _trigger.Show(null);
                _skill.SetVisible(false);
                _trigger.SetVisible(false);
                _name.color = DimLineColor;
                _name.text = BookTexts.Locked(unlockStage);
            }
        }
    }
}
