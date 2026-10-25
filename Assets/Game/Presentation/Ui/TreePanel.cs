using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Strip;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public enum TreeNodeLook
    {
        Normal,
        Offered,
        Taken,
        OwnStart,
        OtherStart,
        Locked
    }

    public sealed class TreePanel
    {
        public const int MaxActions = 4;
        public const int MaxBodyLines = 5;
        public const int CompactBodyLines = 1;
        public const float FullCardMinContentPx = 400f;

        private const float InsetPx = 10f;
        private const float GapPx = 6f;
        private const float PillHeightPx = 28f;
        private const float PillPaddingPx = 12f;
        private const float ToolPx = 32f;
        private const float ToolRadiusPx = 8f;
        private const float CardRadiusPx = 10f;
        private const float CardPaddingPx = 12f;
        private const float CardTopPx = 10f;
        private const float DotPx = 14f;
        private const float TitlePx = 18f;
        private const float BodyPx = 16f;
        private const float StatusPx = 15f;
        private const float ActionHeightPx = 34f;
        private const float ActionWidthPx = 100f;
        private const float ActionRadiusPx = 8f;
        private const float ClickSlopPx = 6f;
        private const float RefreshSeconds = 2f;
        private const float LinkWidth = 0.14f;
        private const float SelectionScale = 2.8f;
        private const double FirstViewHeight = 30;
        private const double PickRadiusShare = 0.03;
        private const double MinPickRadius = 0.6;

        private static readonly Color FieldColor = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color ToolColor = new Color32(0x26, 0x30, 0x4A, 0xF2);
        private static readonly Color PillColor = new Color32(0x1B, 0x22, 0x36, 0xEB);
        private static readonly Color CardColor = new Color32(0x1B, 0x22, 0x36, 0xF5);
        private static readonly Color TakeText = new Color32(0x2A, 0x1C, 0x00, 0xFF);
        private static readonly Color TitleOffered = new Color32(0x8C, 0xE0, 0xA8, 0xFF);
        private static readonly Color BodyColor = new Color32(0xD5, 0xDC, 0xEE, 0xFF);
        private static readonly Color StatusColor = new Color32(0xAE, 0xB9, 0xD6, 0xFF);
        private static readonly Color SelectionColor = new Color32(0xFF, 0xFF, 0xFF, 0xE6);
        private static readonly Color32 NormalColor = new Color32(0x9A, 0xA6, 0xC8, 0x60);
        private static readonly Color32 OfferedColor = new Color32(0x8C, 0xE0, 0xA8, 0xFF);
        private static readonly Color32 TakenColor = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color32 OwnStartColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color32 OtherStartColor = new Color32(0x7A, 0x1E, 0x24, 0xC0);
        private static readonly Color32 LockedColor = new Color32(0x4A, 0x56, 0x77, 0x50);
        private static readonly Color32 LinkDark = new Color32(0xAE, 0xB9, 0xD6, 0x38);
        private static readonly Color32 LinkNear = new Color32(0xAE, 0xB9, 0xD6, 0x90);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly RectTransform _field;
        private readonly RectTransform _map;
        private readonly UiButton _pill;
        private readonly UiButton _zoomIn;
        private readonly UiButton _zoomOut;
        private readonly UiButton _home;
        private readonly RectTransform _card;
        private readonly UnityEngine.UI.Image _dot;
        private readonly TextMeshProUGUI _title;
        private readonly List<TextMeshProUGUI> _body = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _status;
        private readonly List<UiButton> _buttons = new List<UiButton>();
        private readonly List<TreeAction> _actions = new List<TreeAction>();
        private readonly List<string> _info = new List<string>();
        private readonly List<(int From, int To)> _links = new List<(int From, int To)>();
        private TreeMesh? _linkMesh;
        private TreeMesh? _nodeMesh;
        private RectTransform? _selection;
        private GameLoop? _loop;
        private PassiveTree? _tree;
        private TreeViewport? _viewport;
        private TreeNodeDef? _selected;
        private TreeNodeLook[] _looks = System.Array.Empty<TreeNodeLook>();
        private Vector2 _fieldPx = new Vector2(PanelGeometry.WidthPx, PanelGeometry.FullHeightPx);
        private int _takeable;
        private int _bodyLines = MaxBodyLines;
        private bool _pressedInTree;
        private bool _dragged;
        private Vector2 _pressPoint;
        private Vector2 _lastDragPoint;
        private float _sinceRefresh;

        private TreePanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Tree", content));
            _field = UiElements.Fill("Field", _root, FieldColor).rectTransform;
            UiElements.TopEdge(_field, _fieldPx.y);
            _map = UiElements.Rect("Map", _field);
            UiElements.Anchor(_map, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _pill = Tool("Points", new Vector2(0f, 1f), new Vector2(InsetPx, -InsetPx), new Vector2(0f, PillHeightPx), PillColor, PillHeightPx / 2f, theme.TextFont, 12f);
            _pill.Label!.color = BodyColor;
            _pill.Label.alignment = TextAlignmentOptions.Center;
            var right = new Vector2(1f, 1f);
            _zoomIn = Tool("ZoomIn", right, new Vector2(-InsetPx, -InsetPx), Vector2.one * ToolPx, ToolColor, ToolRadiusPx, theme.BoldFont, 18f);
            _zoomIn.Text = "+";
            _zoomOut = Tool("ZoomOut", right, new Vector2(-InsetPx, -InsetPx - ToolPx - GapPx), Vector2.one * ToolPx, ToolColor, ToolRadiusPx, theme.BoldFont, 18f);
            _zoomOut.Text = "−";
            _home = Tool("Home", right, new Vector2(-InsetPx, -InsetPx - 2f * (ToolPx + GapPx)), Vector2.one * ToolPx, ToolColor, ToolRadiusPx);
            var target = UiElements.Ring("Target", _home.Rect, theme.Text);
            UiElements.Anchor(target.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 14f);
            var centre = UiElements.Dot("Centre", _home.Rect, theme.Text);
            UiElements.Anchor(centre.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 5f);
            _card = BuildCard(out _dot, out _title, out _status);
            _root.gameObject.SetActive(false);
        }

        public static TreePanel Create(RectTransform content, UiTheme theme) => new TreePanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public bool IsCompact => _bodyLines == CompactBodyLines;
        public TreeNodeDef? Selected => _selected;
        public string Title => _title.text;
        public string Points => _pill.Label!.text;
        public int ActionCount => _actions.Count;
        public double VisibleHeight => _viewport?.VisibleHeight ?? 0;

        public void Bind(GameLoop loop, PassiveTree tree)
        {
            _loop = loop;
            _tree = tree;
            _takeable = TreeTexts.TakeableCount(tree);
            _looks = new TreeNodeLook[tree.Nodes.Count];
            BuildMap(tree);
            _viewport = new TreeViewport(tree.Nodes, _fieldPx.x / _fieldPx.y);
            FocusOwnPath();
            Refresh();
        }

        public void Show(bool shown, float widthPx, float contentPx)
        {
            if (!shown)
                CancelPress();
            _root.gameObject.SetActive(shown);
            _fieldPx = new Vector2(Mathf.Max(1f, widthPx - 2f * PanelCanvas.BorderPx), Mathf.Max(1f, contentPx - 2f * PanelCanvas.BorderPx));
            UiElements.TopEdge(_field, _fieldPx.y);
            _bodyLines = contentPx >= FullCardMinContentPx ? MaxBodyLines : CompactBodyLines;
            _viewport?.Resize(_fieldPx.x / _fieldPx.y);
            if (!shown)
                return;
            Refresh();
            PlaceMap();
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsShown)
                return;
            _sinceRefresh += deltaSeconds;
            if (_sinceRefresh >= RefreshSeconds)
                Refresh();
        }

        public void ZoomIn()
        {
            _viewport?.ZoomIn();
            PlaceMap();
        }

        public void ZoomOut()
        {
            _viewport?.ZoomOut();
            PlaceMap();
        }

        public void FocusOwnPath()
        {
            if (_viewport == null || _tree == null || _loop == null)
                return;
            var start = _tree.StartOf(_loop.State.Tree.StartClass);
            if (start != null)
                _viewport.FocusOn(start.X, start.Y, FirstViewHeight);
            PlaceMap();
        }

        public Vector2 WorldPointOf(TreeNodeDef node) => _map.TransformPoint(new Vector3((float)node.X, (float)node.Y, 0f));

        public Vector2 WorldPointOfAction(int index) => _buttons[index].Rect.TransformPoint(_buttons[index].Rect.rect.center);

        public Vector2 WorldPointOfZoomIn() => _zoomIn.Rect.TransformPoint(_zoomIn.Rect.rect.center);

        public bool TryPress(Vector2 worldPoint)
        {
            if (!IsShown)
                return false;
            if (Pressed(_zoomIn, worldPoint, ZoomIn) || Pressed(_zoomOut, worldPoint, ZoomOut) || Pressed(_home, worldPoint, FocusOwnPath))
                return true;
            for (var i = 0; i < _actions.Count; i++)
            {
                if (!_buttons[i].Contains(worldPoint))
                    continue;
                Perform(_actions[i]);
                return true;
            }
            if (UiElements.Contains(_card, worldPoint) || UiElements.Contains(_pill.Rect, worldPoint))
                return true;
            if (_viewport == null || !UiElements.Contains(_field, worldPoint))
                return false;
            _pressedInTree = true;
            _dragged = false;
            _pressPoint = worldPoint;
            _lastDragPoint = worldPoint;
            return true;
        }

        public void Drag(Vector2 worldPoint)
        {
            if (!_pressedInTree || _viewport == null)
                return;
            if (!_dragged && (worldPoint - _pressPoint).magnitude * StripSpace.PixelsPerUnit < ClickSlopPx)
                return;
            _dragged = true;
            var from = FieldUv(_lastDragPoint);
            var to = FieldUv(worldPoint);
            _viewport.Pan(to.x - from.x, to.y - from.y);
            _lastDragPoint = worldPoint;
            PlaceMap();
        }

        public void Release(Vector2 worldPoint)
        {
            if (!_pressedInTree)
                return;
            _pressedInTree = false;
            if (_dragged || _viewport == null || _tree == null)
                return;
            var uv = FieldUv(worldPoint);
            var (x, y) = _viewport.ToTree(uv.x, uv.y);
            var radius = System.Math.Max(MinPickRadius, _viewport.VisibleHeight * PickRadiusShare);
            _selected = TreeViewport.NodeAt(_tree.Nodes, x, y, radius);
            ShowSelection();
            ShowInfo();
        }

        public void CancelPress() => _pressedInTree = false;

        public void Refresh()
        {
            _sinceRefresh = 0f;
            if (_loop == null || _tree == null || _nodeMesh == null || _linkMesh == null)
            {
                ShowInfo();
                return;
            }
            var allocation = _loop.State.Tree;
            var offered = new HashSet<TreeNodeDef>(allocation.Allocatable(_tree));
            var canTake = allocation.PointsLeft > 0;
            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                _looks[i] = LookOf(_tree.Nodes[i], allocation, canTake && offered.Contains(_tree.Nodes[i]));
                var centre = new Vector2((float)_tree.Nodes[i].X, (float)_tree.Nodes[i].Y);
                _nodeMesh.SetSquare(i, centre, Radius(_tree.Nodes[i].Type), _looks[i] == TreeNodeLook.Offered ? UiSprites.RingUv : UiSprites.DiscUv);
                _nodeMesh.SetColor(i, ColorOf(_looks[i]));
            }
            for (var i = 0; i < _links.Count; i++)
                _linkMesh.SetColor(i, LinkColor(_looks[_links[i].From], _looks[_links[i].To]));
            _nodeMesh.Apply();
            _linkMesh.Apply();
            ShowInfo();
        }

        public static float Radius(TreeNodeType type) => type switch
        {
            TreeNodeType.Notable => 0.55f,
            TreeNodeType.Keystone => 0.85f,
            TreeNodeType.Mastery => 0.6f,
            TreeNodeType.Jewel => 0.5f,
            TreeNodeType.Start => 1.1f,
            _ => 0.32f
        };

        public TreeNodeLook LookAt(TreeNodeDef node)
        {
            var index = _tree == null ? -1 : IndexOf(_tree, node);
            return index < 0 ? TreeNodeLook.Normal : _looks[index];
        }

        private void BuildMap(PassiveTree tree)
        {
            var index = new Dictionary<TreeNodeDef, int>();
            for (var i = 0; i < tree.Nodes.Count; i++)
                index[tree.Nodes[i]] = i;
            _links.Clear();
            for (var i = 0; i < tree.Nodes.Count; i++)
                foreach (var neighbour in tree.Nodes[i].Neighbours)
                    if (index.TryGetValue(neighbour, out var j) && j > i)
                        _links.Add((i, j));

            var bounds = Bounds(tree);
            _map.sizeDelta = bounds.size;
            _map.pivot = new Vector2(-bounds.xMin / bounds.width, -bounds.yMin / bounds.height);
            _linkMesh = TreeMesh.Create("Links", _map, null, _links.Count);
            for (var i = 0; i < _links.Count; i++)
            {
                var a = Position(tree.Nodes[_links[i].From]);
                var b = Position(tree.Nodes[_links[i].To]);
                var direction = (b - a).normalized;
                var side = new Vector2(-direction.y, direction.x) * (LinkWidth / 2f);
                _linkMesh.SetQuad(i, a - side, a + side, b + side, b - side, new Rect(0f, 0f, 1f, 1f));
            }
            _nodeMesh = TreeMesh.Create("Nodes", _map, UiSprites.NodeAtlas, tree.Nodes.Count);
            _selection = UiElements.Ring("Selection", _nodeMesh.rectTransform, SelectionColor).rectTransform;
            _selection.anchorMin = _selection.anchorMax = _map.pivot;
            _selection.gameObject.SetActive(false);
            _card.SetAsLastSibling();
        }

        private static Rect Bounds(PassiveTree tree)
        {
            var min = Vector2.zero;
            var max = Vector2.zero;
            foreach (var node in tree.Nodes)
            {
                min = Vector2.Min(min, Position(node));
                max = Vector2.Max(max, Position(node));
            }
            var margin = Vector2.one * Radius(TreeNodeType.Start);
            return Rect.MinMaxRect(min.x - margin.x, min.y - margin.y, max.x + margin.x, max.y + margin.y);
        }

        private static Vector2 Position(TreeNodeDef node) => new Vector2((float)node.X, (float)node.Y);

        private void PlaceMap()
        {
            if (_viewport == null)
                return;
            var scale = _fieldPx.y / (float)_viewport.VisibleHeight;
            _map.localScale = new Vector3(scale, scale, 1f);
            _map.anchoredPosition = new Vector2(-(float)_viewport.CentreX * scale, -(float)_viewport.CentreY * scale);
        }

        private Vector2 FieldUv(Vector2 worldPoint)
        {
            var local = (Vector2)_field.InverseTransformPoint(worldPoint);
            var rect = _field.rect;
            return new Vector2((local.x - rect.xMin) / Mathf.Max(1f, rect.width), (local.y - rect.yMin) / Mathf.Max(1f, rect.height));
        }

        private void ShowSelection()
        {
            if (_selection == null)
                return;
            _selection.gameObject.SetActive(_selected != null);
            if (_selected == null)
                return;
            _selection.anchoredPosition = Position(_selected);
            _selection.sizeDelta = Vector2.one * (Radius(_selected.Type) * SelectionScale);
        }

        private void Perform(TreeAction action)
        {
            if (_loop == null || _selected == null)
                return;
            switch (action.Kind)
            {
                case TreeActionKind.Allocate:
                    _loop.AllocateNode(_selected.Id, action.Attribute, action.MasteryOption);
                    break;
                case TreeActionKind.ChangeMastery:
                    if (action.MasteryOption.HasValue)
                        _loop.ChangeMastery(_selected.Id, action.MasteryOption.Value);
                    break;
                case TreeActionKind.Refund:
                    _loop.RefundNode(_selected.Id);
                    break;
            }
            Refresh();
        }

        private TreeNodeLook LookOf(TreeNodeDef node, TreeAllocation allocation, bool offered)
        {
            if (node.Type == TreeNodeType.Start)
                return node.StartClass == allocation.StartClass ? TreeNodeLook.OwnStart : TreeNodeLook.OtherStart;
            if (allocation.IsAllocated(_tree!, node.Id))
                return TreeNodeLook.Taken;
            if (node.Locked)
                return TreeNodeLook.Locked;
            return offered ? TreeNodeLook.Offered : TreeNodeLook.Normal;
        }

        private void ShowInfo()
        {
            _actions.Clear();
            _info.Clear();
            if (_loop == null || _tree == null)
            {
                _pill.Text = string.Empty;
                ShowCard(TreeTexts.Unavailable, _theme.TextDim, Color.clear);
                return;
            }
            var allocation = _loop.State.Tree;
            ShowPoints(allocation);
            if (_selected == null)
            {
                ShowCard(TreeTexts.Hint, StatusColor, Color.clear);
                return;
            }
            _info.AddRange(TreeNodeText.Describe(_tree, allocation, _selected, _loop.RefundCost(_selected.Id), NumberFormatSettings.Default));
            _actions.AddRange(TreeAction.For(_tree, allocation, _selected));
            if (_actions.Count > MaxActions)
                _actions.RemoveRange(MaxActions, _actions.Count - MaxActions);
            var look = LookAt(_selected);
            ShowCard(_info[0], look == TreeNodeLook.Offered ? TitleOffered : look == TreeNodeLook.Taken ? _theme.Gold : _theme.Text, ColorOf(look));
        }

        private void ShowPoints(TreeAllocation allocation)
        {
            var gold = ColorUtility.ToHtmlStringRGB(_theme.Gold);
            _pill.Text = $"<color=#{gold}>{TreeTexts.Points(allocation.PointsLeft)}</color>  {TreeTexts.Taken(allocation.PointsSpent, _takeable)}";
            var width = _pill.Label!.GetPreferredValues(_pill.Label.text).x + 2f * PillPaddingPx;
            _pill.Rect.sizeDelta = new Vector2(width, PillHeightPx);
        }

        private void ShowCard(string title, Color titleColor, Color dotColor)
        {
            _title.text = title;
            _title.color = titleColor;
            _dot.gameObject.SetActive(dotColor.a > 0f);
            _dot.color = dotColor;
            var bodyCount = Mathf.Max(0, Mathf.Min(_info.Count - 2, _bodyLines));
            for (var i = 0; i < _body.Count; i++)
            {
                var shown = i < bodyCount;
                _body[i].gameObject.SetActive(shown);
                if (shown)
                    _body[i].text = _info[i + 1];
            }
            var hasStatus = _info.Count >= 2;
            _status.gameObject.SetActive(hasStatus);
            if (hasStatus)
                _status.text = _info[_info.Count - 1];
            for (var i = 0; i < _buttons.Count; i++)
            {
                var shown = i < _actions.Count;
                _buttons[i].Show(shown);
                if (!shown)
                    continue;
                var primary = _actions[i].Kind != TreeActionKind.Refund;
                _buttons[i].Text = _actions[i].Label;
                _buttons[i].Back.color = primary ? _theme.Gold : _theme.Card;
                _buttons[i].Label!.color = primary ? TakeText : _theme.Text;
            }
            ArrangeCard(bodyCount, hasStatus);
        }

        private void ArrangeCard(int bodyCount, bool hasStatus)
        {
            var textLeft = _dot.gameObject.activeSelf ? CardPaddingPx + DotPx + GapPx * 2f : CardPaddingPx;
            var y = CardTopPx;
            Line(_title.rectTransform, textLeft, ref y, TitlePx);
            for (var i = 0; i < bodyCount; i++)
                Line(_body[i].rectTransform, textLeft, ref y, BodyPx);
            if (hasStatus)
                Line(_status.rectTransform, textLeft, ref y, StatusPx);
            UiElements.Anchor(_dot.rectTransform, new Vector2(0f, 1f), new Vector2(CardPaddingPx, -(CardTopPx + (TitlePx - DotPx) / 2f)), Vector2.one * DotPx);
            if (_actions.Count > 0)
            {
                y += GapPx;
                for (var i = 0; i < _actions.Count; i++)
                {
                    var x = -(CardPaddingPx + (_actions.Count - 1 - i) * (ActionWidthPx + GapPx));
                    UiElements.Anchor(_buttons[i].Rect, new Vector2(1f, 1f), new Vector2(x, -y), new Vector2(ActionWidthPx, ActionHeightPx));
                }
                y += ActionHeightPx;
            }
            y += CardTopPx;
            _card.offsetMin = new Vector2(InsetPx, InsetPx);
            _card.offsetMax = new Vector2(-InsetPx, InsetPx + y);
        }

        private static void Line(RectTransform line, float left, ref float y, float heightPx)
        {
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = Vector2.one;
            line.pivot = new Vector2(0.5f, 1f);
            line.offsetMin = new Vector2(left, -y - heightPx);
            line.offsetMax = new Vector2(-CardPaddingPx, -y);
            y += heightPx;
        }

        private RectTransform BuildCard(out UnityEngine.UI.Image dot, out TextMeshProUGUI title, out TextMeshProUGUI status)
        {
            var border = UiElements.Fill("Card", _field, _theme.Border, CardRadiusPx);
            var card = border.rectTransform;
            card.anchorMin = Vector2.zero;
            card.anchorMax = new Vector2(1f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            UiElements.Stretch(UiElements.Fill("Back", card, CardColor, CardRadiusPx - PanelCanvas.BorderPx).rectTransform, PanelCanvas.BorderPx);
            dot = UiElements.Dot("Dot", card, _theme.Gold);
            title = CardLabel("Title", card, _theme.BoldFont, 14f, _theme.Text);
            for (var i = 0; i < MaxBodyLines; i++)
                _body.Add(CardLabel("Line " + i, card, _theme.TextFont, 12f, BodyColor));
            status = CardLabel("Status", card, _theme.TextFont, 11f, StatusColor);
            for (var i = 0; i < MaxActions; i++)
            {
                var back = UiElements.Fill("Action " + i, card, _theme.Gold, ActionRadiusPx);
                var label = UiElements.Label("Action " + i + "Text", back.rectTransform, _theme.BoldFont, 14f, TakeText, TextAlignmentOptions.Center);
                UiElements.Stretch(label.rectTransform);
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = 14f;
                _buttons.Add(new UiButton(back, label));
            }
            return card;
        }

        private static TextMeshProUGUI CardLabel(string name, RectTransform card, TMP_FontAsset? font, float sizePx, Color color)
        {
            var label = UiElements.Label(name, card, font, sizePx, color, TextAlignmentOptions.MidlineLeft);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private UiButton Tool(string name, Vector2 anchor, Vector2 positionPx, Vector2 sizePx, Color color, float radiusPx, TMP_FontAsset? font = null, float fontSizePx = 0f)
        {
            var back = UiElements.Fill(name, _field, color, radiusPx);
            UiElements.Anchor(back.rectTransform, anchor, positionPx, sizePx);
            if (fontSizePx <= 0f)
                return new UiButton(back, null);
            var label = UiElements.Label(name + "Text", back.rectTransform, font, fontSizePx, _theme.Text, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform);
            return new UiButton(back, label);
        }

        private static bool Pressed(UiButton button, Vector2 worldPoint, System.Action action)
        {
            if (!button.Contains(worldPoint))
                return false;
            action();
            return true;
        }

        private static int IndexOf(PassiveTree tree, TreeNodeDef node)
        {
            for (var i = 0; i < tree.Nodes.Count; i++)
                if (ReferenceEquals(tree.Nodes[i], node))
                    return i;
            return -1;
        }

        private static Color32 ColorOf(TreeNodeLook look) => look switch
        {
            TreeNodeLook.Offered => OfferedColor,
            TreeNodeLook.Taken => TakenColor,
            TreeNodeLook.OwnStart => OwnStartColor,
            TreeNodeLook.OtherStart => OtherStartColor,
            TreeNodeLook.Locked => LockedColor,
            _ => NormalColor
        };

        private static Color32 LinkColor(TreeNodeLook a, TreeNodeLook b)
        {
            var aTaken = a == TreeNodeLook.Taken || a == TreeNodeLook.OwnStart;
            var bTaken = b == TreeNodeLook.Taken || b == TreeNodeLook.OwnStart;
            if (aTaken && bTaken)
                return TakenColor;
            if (aTaken || bTaken)
                return LinkNear;
            return LinkDark;
        }
    }
}
