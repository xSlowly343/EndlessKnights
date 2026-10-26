using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class SubclassPanel
    {
        public const int MaxChoices = 3;

        private const float InsetPx = 10f;
        private const float HeaderHeightPx = 52f;
        private const float HintHeightPx = 34f;
        private const float RadiusPx = 10f;
        private const float MaxPixelsPerUnit = 90f;
        private const float StartPx = 26f;
        private const float NotablePx = 20f;
        private const float SmallPx = 13f;
        private const float RingPx = 2f;
        private const float LinkPx = 2f;
        private const float TakenLinkPx = 3f;
        private const float SelectionGapPx = 5f;
        private const float PickSlackPx = 6f;
        private const float TooltipGapPx = 12f;
        private const float ChoiceHeightPx = 84f;
        private const float ChoiceButtonWidthPx = 92f;
        private const float ChoiceButtonHeightPx = 30f;

        private static readonly Color FieldColor = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color PlateColor = new Color32(0x1B, 0x22, 0x36, 0xEB);
        private static readonly Color DimText = new Color32(0xAE, 0xB9, 0xD6, 0xFF);
        private static readonly Color PathColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color SkillColor = new Color32(0xA9, 0xC4, 0xFF, 0xFF);
        private static readonly Color TakenColor = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color DiskColor = new Color32(0x1B, 0x22, 0x36, 0xFF);
        private static readonly Color OfferedRing = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color LinkColor = new Color32(0xAE, 0xB9, 0xD6, 0x50);
        private static readonly Color SelectionColor = new Color32(0xFF, 0xFF, 0xFF, 0xE6);
        private const float NormalShade = 0.55f;
        private const float LockedShade = 0.25f;

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly RectTransform _field;
        private readonly RectTransform _map;
        private readonly RectTransform _header;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _points;
        private readonly RectTransform _chooser;
        private readonly TextMeshProUGUI _chooserTitle;
        private readonly List<ChoiceRow> _choices = new List<ChoiceRow>();
        private readonly TreeNodeCardView _card;
        private readonly RectTransform _hint;
        private readonly TextMeshProUGUI _hintText;
        private readonly RectTransform _selection;
        private readonly List<NodeView> _nodes = new List<NodeView>();
        private readonly List<LinkView> _links = new List<LinkView>();
        private readonly List<SubclassDef> _offered = new List<SubclassDef>();
        private readonly List<TreeAction> _actions = new List<TreeAction>();
        private readonly Vector3[] _corners = new Vector3[4];
        private TreeNodeCardView? _tooltip;
        private RectTransform? _tooltipPanel;
        private GameLoop? _loop;
        private SubclassDef? _shownPath;
        private TreeNodeDef? _selected;
        private TreeNodeDef? _hovered;
        private Vector2 _fieldPx = new Vector2(PanelGeometry.WidthPx, PanelGeometry.FullHeightPx);
        private float _roomLeftPx;
        private float _roomRightPx;
        private bool _compact;

        private SubclassPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Subclass", content));
            _field = UiElements.Fill("Field", _root, FieldColor).rectTransform;
            UiElements.TopEdge(_field, _fieldPx.y);
            _map = UiElements.Rect("Map", _field);
            UiElements.Anchor(_map, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _selection = UiElements.Ring("Selection", _map, SelectionColor).rectTransform;
            _selection.gameObject.SetActive(false);
            _header = UiElements.Fill("Header", _field, PlateColor, RadiusPx).rectTransform;
            UiElements.Anchor(_header, new Vector2(0f, 1f), new Vector2(InsetPx, -InsetPx), new Vector2(_fieldPx.x - 2f * InsetPx - 0f, HeaderHeightPx));
            _name = Label("Name", _header, theme.BoldFont, 16f, PathColor, TextAlignmentOptions.MidlineLeft);
            Line(_name.rectTransform, 6f, 22f);
            _points = Label("Points", _header, theme.TextFont, 11f, DimText, TextAlignmentOptions.MidlineLeft);
            Line(_points.rectTransform, 28f, 16f);
            _chooser = UiElements.Stretch(UiElements.Rect("Chooser", _field), InsetPx);
            _chooserTitle = Label("ChooserTitle", _chooser, theme.BoldFont, 16f, theme.Gold, TextAlignmentOptions.MidlineLeft);
            _chooserTitle.textWrappingMode = TextWrappingModes.Normal;
            for (var i = 0; i < MaxChoices; i++)
                _choices.Add(BuildChoice(i));
            _card = new TreeNodeCardView("Card", _field, theme, TreeNodeCardMode.Pinned);
            _hint = BuildHint(out _hintText);
            _root.gameObject.SetActive(false);
        }

        public static SubclassPanel Create(RectTransform content, UiTheme theme) => new SubclassPanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public bool IsChoosing => _chooser.gameObject.activeSelf;
        public TreeNodeDef? Selected => _selected;
        public TreeNodeDef? Hovered => _hovered;
        public bool IsTooltipShown => _tooltip != null && _tooltip.IsShown;
        public int ActionCount => _actions.Count;

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            Refresh();
        }

        public void UseTooltip(TreeNodeCardView tooltip, RectTransform panel)
        {
            _tooltip = tooltip;
            _tooltipPanel = panel;
        }

        public void SetRoomBeside(float leftPx, float rightPx)
        {
            _roomLeftPx = leftPx;
            _roomRightPx = rightPx;
        }

        public void Show(bool shown, float widthPx, float contentPx)
        {
            if (!shown)
                HoverAway();
            _root.gameObject.SetActive(shown);
            _fieldPx = new Vector2(Mathf.Max(1f, widthPx - 2f * PanelCanvas.BorderPx), Mathf.Max(1f, contentPx - 2f * PanelCanvas.BorderPx));
            UiElements.TopEdge(_field, _fieldPx.y);
            _header.sizeDelta = new Vector2(_fieldPx.x - 2f * InsetPx, HeaderHeightPx);
            _compact = contentPx < TreePanel.FullCardMinContentPx;
            if (shown)
                Refresh();
        }

        public Vector2 WorldPointOf(TreeNodeDef node) => _map.TransformPoint(PositionOf(node));

        public Vector2 WorldPointOfAction(int index) => _card.Actions[index].Rect.TransformPoint(_card.Actions[index].Rect.rect.center);

        public Vector2 WorldPointOfChoice(int index) => _choices[index].Button.Rect.TransformPoint(_choices[index].Button.Rect.rect.center);

        public bool TryPress(Vector2 worldPoint)
        {
            if (!IsShown || _loop == null)
                return false;
            HoverAway();
            if (IsChoosing)
                return TryChoose(worldPoint);
            for (var i = 0; i < _actions.Count; i++)
            {
                if (!_card.Actions[i].Contains(worldPoint))
                    continue;
                if (_selected != null)
                    _loop.AllocateSubclassNode(_selected.Id);
                Refresh();
                return true;
            }
            if (_card.Close != null && _card.IsShown && _card.Close.Contains(worldPoint))
            {
                Select(null);
                return true;
            }
            if (OverControls(worldPoint))
                return true;
            if (!UiElements.Contains(_field, worldPoint))
                return false;
            Select(NodeAt(worldPoint));
            return true;
        }

        public void Hover(Vector2 worldPoint)
        {
            var node = !IsShown || IsChoosing || OverControls(worldPoint) || !UiElements.Contains(_field, worldPoint) ? null : NodeAt(worldPoint);
            if (node == null)
            {
                HoverAway();
                return;
            }
            if (!ReferenceEquals(node, _hovered) || _tooltip == null || !_tooltip.IsShown)
                ShowTooltip(node);
            PlaceTooltip(node);
        }

        public void HoverAway()
        {
            _hovered = null;
            _tooltip?.Hide();
        }

        public void Refresh()
        {
            if (_loop == null)
                return;
            var catalog = _loop.Data.Ascendancy;
            var state = _loop.State.Ascendancy;
            var path = state.Subclass != null && catalog.TryGet(state.Subclass, out var chosen) ? chosen : null;
            if (!ReferenceEquals(path, _shownPath))
            {
                _shownPath = path;
                _selected = null;
                BuildMap(path);
            }
            _chooser.gameObject.SetActive(path == null);
            _map.gameObject.SetActive(path != null);
            _header.gameObject.SetActive(path != null);
            if (path == null)
            {
                ShowChooser();
                _card.Hide();
                _hint.gameObject.SetActive(false);
                return;
            }
            _name.text = SubclassView.Name(path);
            var skill = SubclassView.PathSkill(_loop.Data.Gems, path);
            _points.text = SubclassView.Points(state, catalog, path) + (skill == null ? string.Empty : " · " + skill);
            PlaceMap(path);
            Paint(path);
            ShowInfo(path);
            if (_hovered != null)
                ShowTooltip(_hovered);
        }

        private void Select(TreeNodeDef? node)
        {
            _selected = node;
            HoverAway();
            Refresh();
        }

        private bool TryChoose(Vector2 worldPoint)
        {
            for (var i = 0; i < _offered.Count && i < _choices.Count; i++)
            {
                if (!_choices[i].Button.Back.gameObject.activeSelf || !_choices[i].Button.Contains(worldPoint))
                    continue;
                _loop!.ChooseSubclass(_offered[i].Id);
                Refresh();
                return true;
            }
            return UiElements.Contains(_field, worldPoint);
        }

        private void ShowChooser()
        {
            var catalog = _loop!.Data.Ascendancy;
            var unlocked = _loop.State.Ascendancy.Unlocked(catalog);
            _offered.Clear();
            _offered.AddRange(catalog.SubclassesOf(_loop.State.Tree.StartClass));
            _chooserTitle.text = unlocked ? Texts.Get("subclass.pick") : SubclassView.Locked(catalog);
            var titleHeight = unlocked ? 24f : 40f;
            UiElements.TopEdge(_chooserTitle.rectTransform, titleHeight);
            for (var i = 0; i < _choices.Count; i++)
            {
                var row = _choices[i];
                var shown = i < _offered.Count;
                row.Back.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var path = _offered[i];
                row.Name.text = SubclassView.Name(path);
                row.Blurb.text = path.Blurb;
                row.Skill.text = SubclassView.PathSkill(_loop.Data.Gems, path) ?? string.Empty;
                row.Button.Show(unlocked);
                UiElements.TopEdge(row.Back.rectTransform, ChoiceHeightPx);
                row.Back.rectTransform.anchoredPosition = new Vector2(0f, -(titleHeight + InsetPx + i * (ChoiceHeightPx + InsetPx)));
            }
        }

        private void ShowInfo(SubclassDef path)
        {
            _actions.Clear();
            if (_selected == null)
            {
                _card.Hide();
                _hint.gameObject.SetActive(true);
                _hintText.text = Texts.Get("subclass.hint");
                return;
            }
            var state = _loop!.State.Ascendancy;
            var catalog = _loop.Data.Ascendancy;
            if (SubclassView.CanTake(state, catalog, _selected))
                _actions.Add(new TreeAction(TreeActionKind.Allocate, Texts.Get("tree.action.take")));
            var card = SubclassView.Card(state, catalog, path, _selected, _loop.Hero);
            _hint.gameObject.SetActive(false);
            _card.Show(_compact ? card.Shortened(TreePanel.CompactBodyLines) : card, _selected.Type, LookOf(_selected), _fieldPx.x - 2f * InsetPx, _actions, string.Empty);
            _card.Root.anchorMin = _card.Root.anchorMax = new Vector2(0.5f, 0f);
            _card.Root.pivot = new Vector2(0.5f, 0f);
            _card.Root.anchoredPosition = new Vector2(0f, InsetPx);
            _card.Root.SetAsLastSibling();
        }

        private void ShowTooltip(TreeNodeDef node)
        {
            _hovered = node;
            if (_tooltip == null || _loop == null || _shownPath == null)
                return;
            var card = SubclassView.Card(_loop.State.Ascendancy, _loop.Data.Ascendancy, _shownPath, node, _loop.Hero);
            _tooltip.Show(card, node.Type, LookOf(node), TreeNodeCardView.TooltipWidthPx, System.Array.Empty<TreeAction>(), Texts.Get("tree.tip.click"));
            _tooltip.Root.SetAsLastSibling();
        }

        private void PlaceTooltip(TreeNodeDef node)
        {
            if (_tooltip == null || _tooltipPanel == null)
                return;
            var layer = (RectTransform)_tooltip.Root.parent;
            _tooltipPanel.GetWorldCorners(_corners);
            var low = layer.InverseTransformPoint(_corners[0]);
            var high = layer.InverseTransformPoint(_corners[2]);
            var anchor = layer.InverseTransformPoint(WorldPointOf(node));
            var width = TreeNodeCardView.TooltipWidthPx;
            var x = TooltipPlacement.Side(_roomLeftPx, _roomRightPx, width, TooltipGapPx) switch
            {
                TooltipSide.Left => low.x - TooltipGapPx - width,
                TooltipSide.Right => high.x + TooltipGapPx,
                _ => anchor.x < (low.x + high.x) / 2f ? high.x - InsetPx - width : low.x + InsetPx
            };
            var top = (float)TooltipPlacement.Top(anchor.y, _tooltip.HeightPx, low.y, high.y);
            _tooltip.Root.anchorMin = _tooltip.Root.anchorMax = Vector2.zero;
            _tooltip.Root.pivot = new Vector2(0f, 1f);
            _tooltip.Root.localPosition = new Vector3(x, top, 0f);
        }

        private TreeNodeLook LookOf(TreeNodeDef node)
        {
            var state = _loop!.State.Ascendancy;
            if (node.Type == TreeNodeType.Start)
                return TreeNodeLook.OwnStart;
            if (state.IsAllocated(node.Id))
                return TreeNodeLook.Taken;
            if (node.Locked)
                return TreeNodeLook.Locked;
            return SubclassView.CanTake(state, _loop.Data.Ascendancy, node) ? TreeNodeLook.Offered : TreeNodeLook.Normal;
        }

        private TreeNodeDef? NodeAt(Vector2 worldPoint)
        {
            TreeNodeDef? best = null;
            var bestDistance = float.MaxValue;
            var local = (Vector2)_map.InverseTransformPoint(worldPoint);
            foreach (var view in _nodes)
            {
                var distance = Vector2.Distance(local, view.Ring.rectTransform.anchoredPosition);
                if (distance > view.SizePx / 2f + PickSlackPx || distance >= bestDistance)
                    continue;
                best = view.Node;
                bestDistance = distance;
            }
            return best;
        }

        private bool OverControls(Vector2 worldPoint) =>
            (_card.IsShown && UiElements.Contains(_card.Root, worldPoint)) || UiElements.Contains(_hint, worldPoint) || UiElements.Contains(_header, worldPoint);

        private void BuildMap(SubclassDef? path)
        {
            foreach (var view in _nodes)
                Object.Destroy(view.Ring.gameObject);
            foreach (var link in _links)
                Object.Destroy(link.Image.gameObject);
            _nodes.Clear();
            _links.Clear();
            if (path == null)
                return;
            var index = new HashSet<(TreeNodeDef, TreeNodeDef)>();
            foreach (var node in path.Nodes)
                foreach (var neighbour in node.Neighbours)
                    if (!index.Contains((neighbour, node)) && index.Add((node, neighbour)))
                        _links.Add(new LinkView(node, neighbour, UiElements.Fill("Link", _map, LinkColor)));
            foreach (var node in path.Nodes)
            {
                var size = SizeOf(node.Type);
                var ring = UiElements.Dot("Node " + node.Id, _map, PathColor);
                var fill = UiElements.Dot("Fill", ring.rectTransform, DiskColor);
                _nodes.Add(new NodeView(node, ring, fill, size));
            }
            _selection.SetAsLastSibling();
        }

        private float _scale = 1f;
        private Vector2 _centre;

        private void PlaceMap(SubclassDef path)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var node in path.Nodes)
            {
                var p = new Vector2((float)node.X, (float)node.Y);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            var top = HeaderHeightPx + 2f * InsetPx;
            var bottom = (_selected != null && _card.IsShown ? _card.HeightPx : HintHeightPx) + 2f * InsetPx + StartPx;
            var area = new Vector2(_fieldPx.x - 2f * InsetPx - StartPx, Mathf.Max(1f, _fieldPx.y - top - bottom));
            var span = Vector2.Max(max - min, Vector2.one * 0.01f);
            _scale = Mathf.Min(MaxPixelsPerUnit, Mathf.Min(area.x / span.x, area.y / span.y));
            _centre = (min + max) / 2f;
            _map.anchoredPosition = new Vector2(0f, (bottom - top) / 2f);
        }

        private Vector2 PositionOf(TreeNodeDef node) => (new Vector2((float)node.X, -(float)node.Y) - new Vector2(_centre.x, -_centre.y)) * _scale;

        private void Paint(SubclassDef path)
        {
            foreach (var view in _nodes)
            {
                var look = LookOf(view.Node);
                var rect = view.Ring.rectTransform;
                UiElements.Anchor(rect, new Vector2(0.5f, 0.5f), PositionOf(view.Node), Vector2.one * view.SizePx);
                view.Fill.rectTransform.anchorMin = Vector2.zero;
                view.Fill.rectTransform.anchorMax = Vector2.one;
                view.Fill.rectTransform.offsetMin = Vector2.one * RingPx;
                view.Fill.rectTransform.offsetMax = -Vector2.one * RingPx;
                view.Ring.color = look switch
                {
                    TreeNodeLook.Taken or TreeNodeLook.OwnStart => TakenColor,
                    TreeNodeLook.Offered => OfferedRing,
                    TreeNodeLook.Locked => Shade(PathColor, LockedShade),
                    _ => Shade(PathColor, NormalShade)
                };
                view.Fill.color = look == TreeNodeLook.Taken || look == TreeNodeLook.OwnStart ? PathColor : DiskColor;
            }
            foreach (var link in _links)
            {
                var a = PositionOf(link.From);
                var b = PositionOf(link.To);
                var taken = SubclassView.IsTaken(_loop!.State.Ascendancy, link.From) && SubclassView.IsTaken(_loop.State.Ascendancy, link.To);
                var rect = link.Image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = a;
                rect.sizeDelta = new Vector2((b - a).magnitude, taken ? TakenLinkPx : LinkPx);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                link.Image.color = taken ? TakenColor : LinkColor;
            }
            _selection.gameObject.SetActive(_selected != null);
            if (_selected != null)
                UiElements.Anchor(_selection, new Vector2(0.5f, 0.5f), PositionOf(_selected), Vector2.one * (SizeOf(_selected.Type) + 2f * SelectionGapPx));
        }

        private static float SizeOf(TreeNodeType type) => type switch
        {
            TreeNodeType.Start => StartPx,
            TreeNodeType.Notable or TreeNodeType.Keystone => NotablePx,
            _ => SmallPx
        };

        private static Color Shade(Color color, float share) => new Color(color.r * share, color.g * share, color.b * share, color.a);

        private ChoiceRow BuildChoice(int index)
        {
            var back = UiElements.Fill("Choice " + index, _chooser, PlateColor, RadiusPx);
            var name = Label("Name", back.rectTransform, _theme.BoldFont, 15f, PathColor, TextAlignmentOptions.MidlineLeft);
            Line(name.rectTransform, 8f, 20f, ChoiceButtonWidthPx + 2f * InsetPx);
            var blurb = Label("Blurb", back.rectTransform, _theme.TextFont, 12f, DimText, TextAlignmentOptions.TopLeft);
            blurb.textWrappingMode = TextWrappingModes.Normal;
            Line(blurb.rectTransform, 30f, 32f, ChoiceButtonWidthPx + 2f * InsetPx);
            var skill = Label("Skill", back.rectTransform, _theme.TextFont, 11f, SkillColor, TextAlignmentOptions.MidlineLeft);
            Line(skill.rectTransform, 62f, 16f, ChoiceButtonWidthPx + 2f * InsetPx);
            var button = UiElements.Raise(UiElements.Fill("Choose", back.rectTransform, _theme.Gold, 6f));
            UiElements.Anchor(button.rectTransform, new Vector2(1f, 0.5f), new Vector2(-InsetPx, 0f), new Vector2(ChoiceButtonWidthPx, ChoiceButtonHeightPx));
            var label = UiElements.Label("ChooseText", button.rectTransform, _theme.BoldFont, 13f, _theme.TextOnGold, TextAlignmentOptions.Center);
            label.text = Texts.Get("subclass.choose");
            UiElements.Stretch(label.rectTransform);
            return new ChoiceRow(back, name, blurb, skill, new UiButton(button, label));
        }

        private RectTransform BuildHint(out TextMeshProUGUI text)
        {
            var back = UiElements.Fill("Hint", _field, PlateColor, RadiusPx);
            var rect = UiElements.BottomEdge(back.rectTransform, HintHeightPx, InsetPx);
            rect.anchoredPosition = new Vector2(0f, InsetPx);
            text = Label("HintText", rect, _theme.TextFont, 12f, DimText, TextAlignmentOptions.Center);
            UiElements.Stretch(text.rectTransform, 8f);
            return rect;
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, TMP_FontAsset? font, float sizePx, Color color, TextAlignmentOptions alignment)
        {
            var label = UiElements.Label(name, parent, font, sizePx, color, alignment);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static void Line(RectTransform rect, float topPx, float heightPx, float rightPx = 12f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12f, -topPx - heightPx);
            rect.offsetMax = new Vector2(-rightPx, -topPx);
        }

        private readonly struct NodeView
        {
            public readonly TreeNodeDef Node;
            public readonly Image Ring;
            public readonly Image Fill;
            public readonly float SizePx;

            public NodeView(TreeNodeDef node, Image ring, Image fill, float sizePx)
            {
                Node = node;
                Ring = ring;
                Fill = fill;
                SizePx = sizePx;
            }
        }

        private readonly struct LinkView
        {
            public readonly TreeNodeDef From;
            public readonly TreeNodeDef To;
            public readonly Image Image;

            public LinkView(TreeNodeDef from, TreeNodeDef to, Image image)
            {
                From = from;
                To = to;
                Image = image;
            }
        }

        private readonly struct ChoiceRow
        {
            public readonly Image Back;
            public readonly TextMeshProUGUI Name;
            public readonly TextMeshProUGUI Blurb;
            public readonly TextMeshProUGUI Skill;
            public readonly UiButton Button;

            public ChoiceRow(Image back, TextMeshProUGUI name, TextMeshProUGUI blurb, TextMeshProUGUI skill, UiButton button)
            {
                Back = back;
                Name = name;
                Blurb = blurb;
                Skill = skill;
                Button = button;
            }
        }
    }
}
