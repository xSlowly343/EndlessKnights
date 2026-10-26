using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class MasterPanel
    {
        public const int Places = 7;

        private const float PaddingPx = 16f;
        private const float TopPx = 4f;
        private const float GapPx = 6f;
        private const float HandHeightPx = 26f;
        private const float HandPaddingPx = 12f;
        private const float CircleTopPx = TopPx + HandHeightPx + GapPx;
        private const float CircleHeightPx = 232f;
        private const float RadiusPx = 92f;
        private const float NodeWidthPx = 80f;
        private const float NodeHeightPx = 68f;
        private const float GemCenterPx = 18f;
        private const float LabelTopPx = 40f;
        private const float GemPx = 20f;
        private const float RingPx = 2f;
        private const float SelectedRingPx = 3f;
        private const float LegendHeightPx = 18f;
        private const float LegendDotPx = 8f;
        private const float CardHeightPx = 140f;
        private const float CardRadiusPx = 12f;
        private const float ButtonHeightPx = 32f;
        private const float ButtonRadiusPx = 8f;

        private static readonly Color[] TierColors =
        {
            new Color32(0x9A, 0xA3, 0xB5, 0xFF),
            new Color32(0x5B, 0x9B, 0xFF, 0xFF),
            new Color32(0xB0, 0x7C, 0xFF, 0xFF),
            new Color32(0xFF, 0x9A, 0x3C, 0xFF),
            new Color32(0xFF, 0x5A, 0x5A, 0xFF)
        };
        private static readonly Color ClosedFill = new Color32(0x2A, 0x30, 0x45, 0x99);
        private static readonly Color ClosedText = new Color32(0x8A, 0x93, 0xAE, 0xFF);
        private static readonly Color ActiveHandFill = new Color32(0x3A, 0x2E, 0x14, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<UiButton> _handButtons = new List<UiButton>();
        private readonly List<WeaponHand> _hands = new List<WeaponHand>();
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<MasterPlace> _places = new List<MasterPlace>();
        private readonly TextMeshProUGUI _noWeapon;
        private readonly RectTransform _card;
        private TextMeshProUGUI _name = null!;
        private TextMeshProUGUI _line = null!;
        private TextMeshProUGUI _tier = null!;
        private TextMeshProUGUI _result = null!;
        private UiButton _action = null!;
        private WeaponHand _hand = WeaponHand.Right;
        private int _selected = -1;
        private GameLoop? _loop;

        private MasterPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Master", content));
            BuildHands();
            BuildCircle();
            BuildLegend();
            _noWeapon = UiElements.Label("NoWeapon", _root, theme.TextFont, 14f, theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(_noWeapon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(CircleTopPx + CircleHeightPx / 2f)), new Vector2(300f, 24f));
            _noWeapon.text = MasterTexts.NoWeapon;
            _card = BuildCard();
            _root.gameObject.SetActive(false);
        }

        public static MasterPanel Create(RectTransform content, UiTheme theme) => new MasterPanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public int Selected => _selected;

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
            var state = _loop.State;
            MasterCircle.Hands(state, _hands);
            if (_hands.Count > 0 && !_hands.Contains(_hand))
                _hand = _hands[0];
            ShowHands(state);
            var weapon = _hands.Count > 0 ? state.Weapons.In(_hand) : null;
            _places.Clear();
            if (weapon != null)
                MasterCircle.Fill(weapon, state.HeroLevel, _loop.Data.Weapons.SkillRules.UnlockLevels, _loop.Data.Gems, _loop.Book, _places);
            if (_selected >= _places.Count || (_selected >= 0 && !_places[_selected].Open))
                _selected = -1;
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (i < _places.Count)
                    _nodes[i].Show(_places[i], i == _selected, _theme);
                else
                    _nodes[i].Hide();
            }
            _noWeapon.gameObject.SetActive(weapon == null);
            ShowCard();
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown || _loop == null)
                return false;
            for (var i = 0; i < _handButtons.Count; i++)
            {
                if (!_handButtons[i].Rect.gameObject.activeSelf || !_handButtons[i].Contains(worldPoint))
                    continue;
                _hand = _hands[i];
                _selected = -1;
                Refresh();
                return true;
            }
            if (_selected >= 0 && TryAct(worldPoint))
                return true;
            for (var i = 0; i < _places.Count; i++)
            {
                if (!_nodes[i].Contains(worldPoint) || !_places[i].Open)
                    continue;
                _selected = i == _selected ? -1 : i;
                _result.text = string.Empty;
                Refresh();
                return true;
            }
            return false;
        }

        public Vector2 WorldPointOfPlace(int index) => _nodes[index].Rect.TransformPoint(_nodes[index].Rect.rect.center);

        private bool TryAct(Vector2 worldPoint)
        {
            if (!_action.Contains(worldPoint))
                return UiElements.Contains(_card, worldPoint);
            var place = _places[_selected];
            var result = place.InBook
                ? _loop!.Book.Unequip(place.BookRow)
                : _loop!.Book.PlaceFromWeapon(place.Gem, _loop.Data.Weapons.SkillRules.UnlockLevels);
            _result.text = BookTexts.Result(result);
            Refresh();
            return true;
        }

        private void ShowHands(GameState state)
        {
            for (var i = 0; i < _handButtons.Count; i++)
            {
                var shown = i < _hands.Count;
                _handButtons[i].Show(shown);
                if (!shown)
                    continue;
                var active = _hands[i] == _hand;
                _handButtons[i].Text = MasterTexts.Hand(state.Weapons.In(_hands[i])!);
                _handButtons[i].Back.color = active ? _theme.Gold : _theme.Border;
                _handButtons[i].Rect.GetChild(0).GetComponent<Image>().color = active ? ActiveHandFill : _theme.Background;
                _handButtons[i].Label!.color = active ? _theme.Gold : _theme.Text;
            }
        }

        private void ShowCard()
        {
            var shown = _selected >= 0;
            _card.gameObject.SetActive(shown);
            if (!shown)
                return;
            var place = _places[_selected];
            var skill = place.Gem.Skill!;
            _name.text = BookTexts.Name(place.Gem);
            _line.text = MasterTexts.Line(skill);
            _tier.text = MasterTexts.Tier(skill.Tier);
            _tier.color = TierColors[(int)skill.Tier];
            _action.Text = place.InBook ? MasterTexts.Remove : MasterTexts.Place;
        }

        private void BuildHands()
        {
            var row = UiElements.Anchor(UiElements.Rect("Hands", _root), new Vector2(0f, 1f), new Vector2(PaddingPx, -TopPx), new Vector2(Width(), HandHeightPx));
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = GapPx;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            for (var i = 0; i < 2; i++)
            {
                var border = UiElements.Fill("Hand", row, _theme.Border, HandHeightPx / 2f);
                var fit = border.gameObject.AddComponent<HorizontalLayoutGroup>();
                fit.padding = new RectOffset((int)HandPaddingPx, (int)HandPaddingPx, 0, 0);
                fit.childControlWidth = true;
                fit.childControlHeight = true;
                fit.childForceExpandWidth = false;
                border.gameObject.AddComponent<LayoutElement>().preferredHeight = HandHeightPx;
                var inner = UiElements.Fill("Inner", border.rectTransform, _theme.Background, HandHeightPx / 2f - 1f);
                UiElements.IgnoreLayout(inner.rectTransform);
                UiElements.Stretch(inner.rectTransform, 1f);
                var label = UiElements.Label("Text", border.rectTransform, _theme.TextFont, 12f, _theme.Text, TextAlignmentOptions.Center);
                _handButtons.Add(new UiButton(border, label));
            }
        }

        private void BuildCircle()
        {
            var center = new Vector2(PaddingPx + Width() / 2f, -(CircleTopPx + CircleHeightPx / 2f));
            for (var i = 0; i < Places; i++)
            {
                var (x, y) = MasterCircle.Position(i, Places, RadiusPx);
                _nodes.Add(Node.Create(_root, center + new Vector2((float)x, -(float)y), _theme));
            }
        }

        private void BuildLegend()
        {
            var row = UiElements.Anchor(UiElements.Rect("Legend", _root), new Vector2(0f, 1f), new Vector2(PaddingPx, -(CircleTopPx + CircleHeightPx + 4f)), new Vector2(Width(), LegendHeightPx));
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 12f;
            line.childAlignment = TextAnchor.MiddleCenter;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            for (var tier = SkillTier.Common; tier <= SkillTier.Mythic; tier++)
            {
                var item = UiElements.Rect("Tier", row);
                var group = item.gameObject.AddComponent<HorizontalLayoutGroup>();
                group.spacing = 5f;
                group.childAlignment = TextAnchor.MiddleLeft;
                group.childControlWidth = true;
                group.childControlHeight = false;
                group.childForceExpandWidth = false;
                var dot = UiElements.Fill("Dot", item, TierColors[(int)tier], 2f);
                dot.rectTransform.sizeDelta = Vector2.one * LegendDotPx;
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                dot.gameObject.AddComponent<LayoutElement>().preferredWidth = LegendDotPx;
                var label = UiElements.Label("Text", item, _theme.TextFont, 11f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
                label.text = MasterTexts.Tier(tier);
            }
        }

        private RectTransform BuildCard()
        {
            var card = UiElements.Anchor(UiElements.Rect("Card", _root), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(Width(), CardHeightPx));
            var back = UiElements.Fill("Back", card, _theme.Card, CardRadiusPx);
            UiElements.Stretch(back.rectTransform);
            _name = CardText("Name", card, _theme.BoldFont, 15f, _theme.Text, new Vector2(12f, -10f), new Vector2(Width() - 130f, 20f), TextAlignmentOptions.MidlineLeft);
            _tier = CardText("Tier", card, _theme.BoldFont, 12f, TierColors[0], new Vector2(Width() - 112f, -10f), new Vector2(100f, 20f), TextAlignmentOptions.MidlineRight);
            _line = CardText("Line", card, _theme.TextFont, 12f, _theme.TextDim, new Vector2(12f, -32f), new Vector2(Width() - 24f, 18f), TextAlignmentOptions.MidlineLeft);
            _result = CardText("Result", card, _theme.TextFont, 11f, _theme.Alert, new Vector2(12f, -56f), new Vector2(Width() - 24f, 32f), TextAlignmentOptions.TopLeft);
            _result.textWrappingMode = TextWrappingModes.Normal;
            _action = GoldButton(card, new Vector2(12f, 10f), Width() - 24f);
            card.gameObject.SetActive(false);
            return card;
        }

        private static TextMeshProUGUI CardText(string name, RectTransform card, TMP_FontAsset? font, float size, Color color, Vector2 position, Vector2 box, TextAlignmentOptions alignment)
        {
            var label = UiElements.Label(name, card, font, size, color, alignment);
            UiElements.Anchor(label.rectTransform, new Vector2(0f, 1f), position, box);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private UiButton GoldButton(RectTransform card, Vector2 position, float width)
        {
            var shade = UiElements.Fill("Action", card, new Color32(0xB4, 0x7E, 0x1A, 0xFF), ButtonRadiusPx);
            UiElements.Anchor(shade.rectTransform, Vector2.zero, position, new Vector2(width, ButtonHeightPx));
            var face = UiElements.Fill("Face", shade.rectTransform, _theme.Gold, ButtonRadiusPx);
            UiElements.Stretch(face.rectTransform).offsetMin = new Vector2(0f, 3f);
            var label = UiElements.Label("Text", shade.rectTransform, _theme.BoldFont, 12f, new Color32(0x2A, 0x1C, 0x00, 0xFF), TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform).offsetMin = new Vector2(0f, 3f);
            return new UiButton(shade, label);
        }

        private static float Width() => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;

        private sealed class Node
        {
            private readonly RectTransform _box;
            private readonly Image _ring;
            private readonly Image _fill;
            private readonly TextMeshProUGUI _label;
            private readonly Image _inBook;

            private Node(RectTransform box, Image ring, Image fill, TextMeshProUGUI label, Image inBook)
            {
                _box = box;
                _ring = ring;
                _fill = fill;
                _label = label;
                _inBook = inBook;
            }

            public RectTransform Rect => _box;

            public static Node Create(RectTransform parent, Vector2 center, UiTheme theme)
            {
                var box = UiElements.Anchor(UiElements.Rect("Place", parent), new Vector2(0f, 1f), center + new Vector2(-NodeWidthPx / 2f, GemCenterPx), new Vector2(NodeWidthPx, NodeHeightPx));
                var gem = UiElements.Rect("Gem", box);
                gem.anchorMin = gem.anchorMax = new Vector2(0.5f, 1f);
                gem.pivot = new Vector2(0.5f, 0.5f);
                gem.anchoredPosition = new Vector2(0f, -GemCenterPx);
                gem.sizeDelta = Vector2.one * (GemPx + 2f * SelectedRingPx);
                gem.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var ring = UiElements.Fill("Ring", gem, theme.Border, 5f);
                UiElements.Stretch(ring.rectTransform);
                var fill = UiElements.Fill("Fill", ring.rectTransform, theme.Text, 4f);
                UiElements.Stretch(fill.rectTransform, SelectedRingPx);
                var label = UiElements.Label("Name", box, theme.BoldFont, 11f, theme.Text, TextAlignmentOptions.Top);
                UiElements.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -LabelTopPx), new Vector2(NodeWidthPx, 28f));
                label.textWrappingMode = TextWrappingModes.Normal;
                var inBook = UiElements.Dot("InBook", box, theme.Gold);
                UiElements.Anchor(inBook.rectTransform, new Vector2(0.5f, 1f), new Vector2(GemPx + 4f, -GemCenterPx + 3f), Vector2.one * 6f);
                return new Node(box, ring, fill, label, inBook);
            }

            public bool Contains(Vector2 worldPoint) => _box.gameObject.activeSelf && UiElements.Contains(_box, worldPoint);

            public void Show(MasterPlace place, bool selected, UiTheme theme)
            {
                _box.gameObject.SetActive(true);
                var skill = place.Gem.Skill!;
                var open = place.Open && !place.Gem.Locked;
                _label.text = BookTexts.Name(place.Gem);
                _label.color = open ? theme.Text : ClosedText;
                _fill.color = open ? GemColors.Of(place.Gem.Color) : ClosedFill;
                _ring.color = selected ? Color.white : open ? TierColors[(int)skill.Tier] : ClosedFill;
                _fill.rectTransform.offsetMin = Vector2.one * (selected ? SelectedRingPx : RingPx);
                _fill.rectTransform.offsetMax = -Vector2.one * (selected ? SelectedRingPx : RingPx);
                _inBook.gameObject.SetActive(place.InBook);
            }

            public void Hide() => _box.gameObject.SetActive(false);
        }
    }
}
