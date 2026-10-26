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
        public const int MaxActions = TreeNodeCardView.MaxActions;
        public const int MaxBodyLines = TreeNodeCardView.MaxStatLines;
        public const int CompactBodyLines = 1;
        public const float FullCardMinContentPx = 400f;

        private const string GlyphGroup = "tree";
        private const float GlyphShare = 0.62f;
        private const float CompactGlyphShare = 0.8f;
        private const float CompactNodePx = 20f;
        private const float InsetPx = 10f;
        private const float GapPx = 6f;
        private const float PillHeightPx = 28f;
        private const float PillPaddingPx = 12f;
        private const float ToolPx = 32f;
        private const float ToolRadiusPx = 8f;
        private const float ClickSlopPx = 6f;
        private const float HintHeightPx = 34f;
        private const float HintRadiusPx = 10f;
        private const float TooltipGapPx = 12f;
        private const float RefreshSeconds = 2f;
        private const float LinkWorld = 0.06f;
        private const float LinkPx = 1.5f;
        private const float TakenLinkWorld = 0.14f;
        private const float TakenLinkPx = 3f;
        private const float SelectionScale = 1.5f;
        private const float SelectionGapPx = 4f;
        private const float MarkerGapPx = 2f;
        private const double PickRadiusShare = 0.03;
        private const double MinPickRadius = 0.6;

        private static readonly Color FieldColor = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color ToolColor = new Color32(0x26, 0x30, 0x4A, 0xF2);
        private static readonly Color PillColor = new Color32(0x1B, 0x22, 0x36, 0xEB);
        private static readonly Color StatusColor = new Color32(0xAE, 0xB9, 0xD6, 0xFF);
        private static readonly Color HintColor = new Color32(0x1B, 0x22, 0x36, 0xEB);
        private static readonly Color SelectionColor = new Color32(0xFF, 0xFF, 0xFF, 0xE6);
        private static readonly Color32 RedColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color32 GreenColor = new Color32(0x4F, 0xC2, 0x77, 0xFF);
        private static readonly Color32 BlueColor = new Color32(0x3F, 0x6F, 0xD1, 0xFF);
        private static readonly Color32 WhiteColor = new Color32(0xE6, 0xD3, 0xA3, 0xFF);
        private static readonly Color32 TakenColor = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color32 DiskColor = new Color32(0x14, 0x19, 0x2C, 0xFF);
        private static readonly Color32 OfferedRing = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 Hidden = new Color32(0, 0, 0, 0);
        private const float NormalShade = 0.72f;
        private const float LockedShade = 0.3f;
        private const float OtherStartShade = 0.5f;
        private const float OfferedGlyphLift = 0.35f;
        private const float NormalGlyphLift = 0.15f;
        private const byte NotableOuterAlpha = 0x90;
        private const float OutlineScale = 1.3f;
        private const float MaxPixelsPerUnit = 25f;
        private static readonly Color32 LinkDark = new Color32(0xAE, 0xB9, 0xD6, 0x50);
        private static readonly Color32 LinkNear = new Color32(0xAE, 0xB9, 0xD6, 0x90);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly RectTransform _field;
        private readonly RectTransform _map;
        private readonly UiButton _pill;
        private readonly UiButton _zoomIn;
        private readonly UiButton _zoomOut;
        private readonly UiButton _home;
        private readonly TreeNodeCardView _card;
        private readonly RectTransform _hint;
        private readonly TextMeshProUGUI _hintText;
        private readonly List<TreeAction> _actions = new List<TreeAction>();
        private TreeNodeCardView? _tooltip;
        private RectTransform? _tooltipPanel;
        private TreeNodeDef? _hovered;
        private float _roomLeftPx;
        private float _roomRightPx;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<(int From, int To)> _links = new List<(int From, int To)>();
        private TreeMesh? _linkMesh;
        private TreeMesh? _nodeMesh;
        private TreeMesh? _ringMesh;
        private TreeMesh? _outlineMesh;
        private TreeMarkers? _markers;
        private double[] _halves = System.Array.Empty<double>();
        private int[] _priorities = System.Array.Empty<int>();
        private readonly List<TreeNodeDef> _shownNodes = new List<TreeNodeDef>();
        private readonly List<int> _outlined = new List<int>();
        private Color32[] _sectorColors = System.Array.Empty<Color32>();
        private readonly List<GlyphLayer> _glyphLayers = new List<GlyphLayer>();
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
        private float _glyphScale;

        private TreePanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Tree", content));
            _field = UiElements.Fill("Field", _root, FieldColor).rectTransform;
            UiElements.TopEdge(_field, _fieldPx.y);
            _map = UiElements.Rect("Map", _field);
            UiElements.Anchor(_map, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _pill = Tool("Points", new Vector2(0f, 1f), new Vector2(InsetPx, -InsetPx), new Vector2(0f, PillHeightPx), PillColor, PillHeightPx / 2f, theme.TextFont, 12f);
            _pill.Label!.color = StatusColor;
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
            _card = new TreeNodeCardView("Card", _field, theme, TreeNodeCardMode.Pinned);
            _hint = BuildHint(out _hintText);
            _root.gameObject.SetActive(false);
        }

        public static TreePanel Create(RectTransform content, UiTheme theme) => new TreePanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public bool IsCompact => _bodyLines == CompactBodyLines;
        public TreeNodeDef? Selected => _selected;
        public string Title => _selected == null ? _hintText.text : Game.Core.Text.Texts.Pick(_selected.NameRu, _selected.NameEn);
        public TreeNodeDef? Hovered => _hovered;
        public bool IsTooltipShown => _tooltip != null && _tooltip.IsShown;
        public string Points => _pill.Label!.text;
        public int ActionCount => _actions.Count;
        public double VisibleHeight => _viewport?.VisibleHeight ?? 0;

        public void Bind(GameLoop loop, PassiveTree tree)
        {
            _loop = loop;
            _tree = tree;
            _takeable = TreeTexts.TakeableCount(tree);
            _looks = new TreeNodeLook[tree.Nodes.Count];
            _sectorColors = new Color32[tree.Nodes.Count];
            _halves = new double[tree.Nodes.Count];
            _priorities = new int[tree.Nodes.Count];
            for (var i = 0; i < tree.Nodes.Count; i++)
                _sectorColors[i] = SectorColor(tree.Nodes[i]);
            _markers = new TreeMarkers(tree.Nodes);
            BuildMap(tree);
            _viewport = new TreeViewport(tree.Nodes, _fieldPx.x / _fieldPx.y);
            _viewport.LimitZoom(_fieldPx.y / MaxPixelsPerUnit);
            FocusOwnPath();
            Refresh();
        }

        public void Show(bool shown, float widthPx, float contentPx)
        {
            if (!shown)
            {
                CancelPress();
                HoverAway();
            }
            _root.gameObject.SetActive(shown);
            _fieldPx = new Vector2(Mathf.Max(1f, widthPx - 2f * PanelCanvas.BorderPx), Mathf.Max(1f, contentPx - 2f * PanelCanvas.BorderPx));
            UiElements.TopEdge(_field, _fieldPx.y);
            _bodyLines = contentPx >= FullCardMinContentPx ? MaxBodyLines : CompactBodyLines;
            _viewport?.Resize(_fieldPx.x / _fieldPx.y);
            _viewport?.LimitZoom(_fieldPx.y / MaxPixelsPerUnit);
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
                _viewport.FocusOn(start.X, start.Y, _fieldPx.y / MaxPixelsPerUnit);
            PlaceMap();
        }

        public Vector2 WorldPointOf(TreeNodeDef node) => _map.TransformPoint(new Vector3((float)node.X, (float)node.Y, 0f));

        public Vector2 WorldPointOfAction(int index) => _card.Actions[index].Rect.TransformPoint(_card.Actions[index].Rect.rect.center);

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

        public void Hover(Vector2 worldPoint)
        {
            var node = !IsShown || _pressedInTree || OverControls(worldPoint) || !UiElements.Contains(_field, worldPoint) ? null : NodeAt(worldPoint);
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

        public Vector2 WorldPointOfZoomIn() => _zoomIn.Rect.TransformPoint(_zoomIn.Rect.rect.center);

        public bool TryPress(Vector2 worldPoint)
        {
            if (!IsShown)
                return false;
            if (Pressed(_zoomIn, worldPoint, ZoomIn) || Pressed(_zoomOut, worldPoint, ZoomOut) || Pressed(_home, worldPoint, FocusOwnPath))
                return true;
            for (var i = 0; i < _actions.Count; i++)
            {
                if (!_card.Actions[i].Contains(worldPoint))
                    continue;
                Perform(_actions[i]);
                return true;
            }
            if (_card.Close != null && _card.IsShown && _card.Close.Contains(worldPoint))
            {
                Select(null);
                return true;
            }
            if (OverControls(worldPoint))
                return true;
            HoverAway();
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
            Select(NodeAt(worldPoint));
        }

        private TreeNodeDef? NodeAt(Vector2 worldPoint)
        {
            if (_viewport == null)
                return null;
            var uv = FieldUv(worldPoint);
            var (x, y) = _viewport.ToTree(uv.x, uv.y);
            var radius = System.Math.Max(MinPickRadius, _viewport.VisibleHeight * PickRadiusShare);
            return TreeViewport.NodeAt(_shownNodes, x, y, radius);
        }

        private bool OverControls(Vector2 worldPoint) =>
            (_card.IsShown && UiElements.Contains(_card.Root, worldPoint)) || UiElements.Contains(_hint, worldPoint) || UiElements.Contains(_pill.Rect, worldPoint)
            || _zoomIn.Contains(worldPoint) || _zoomOut.Contains(worldPoint) || _home.Contains(worldPoint);

        private void ShowTooltip(TreeNodeDef node)
        {
            _hovered = node;
            if (_tooltip == null || _loop == null || _tree == null)
                return;
            var card = TreeNodeCard.ForPassive(_tree, _loop.State.Tree, node, _loop.RefundCost(node.Id), NumberDisplay.Settings, _loop.Hero);
            _tooltip.Show(card, node.Type, LookAt(node), TreeNodeCardView.TooltipWidthPx, System.Array.Empty<TreeAction>(), Game.Core.Text.Texts.Get("tree.tip.click"));
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
            var side = TooltipPlacement.Side(_roomLeftPx, _roomRightPx, width, TooltipGapPx);
            var x = side switch
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

        public bool IsMarkerShown(TreeNodeDef node)
        {
            var index = _tree == null || _markers == null ? -1 : IndexOf(_tree, node);
            return index >= 0 && _markers!.IsShown(index);
        }

        private void Select(TreeNodeDef? node)
        {
            _selected = node;
            HoverAway();
            Paint();
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
                _looks[i] = LookOf(_tree.Nodes[i], allocation, canTake && offered.Contains(_tree.Nodes[i]));
            Paint();
            ShowInfo();
            if (_hovered != null)
                ShowTooltip(_hovered);
        }

        private void Paint()
        {
            if (_tree == null || _nodeMesh == null || _ringMesh == null || _markers == null)
                return;
            var pixelsPerUnit = PixelsPerUnit();
            PickMarkers();
            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                var node = _tree.Nodes[i];
                var position = Position(node);
                var half = (float)_halves[i];
                var shown = _markers.IsShown(i);
                _nodeMesh.SetSquare(i, position, half, ShapeOf(node.Type, false));
                _nodeMesh.SetColor(i, shown ? DiskColor : Hidden);
                _ringMesh.SetSquare(i, position, half, ShapeOf(node.Type, true));
                _ringMesh.SetColor(i, shown ? RingColorOf(_looks[i], _sectorColors[i]) : Hidden);
            }
            _nodeMesh.Apply();
            _ringMesh.Apply();
            PaintLinks(pixelsPerUnit);
            PaintOutlines();
            PaintGlyphs(pixelsPerUnit);
            ShowSelection();
        }

        private void PickMarkers()
        {
            if (_tree == null || _markers == null)
                return;
            _shownNodes.Clear();
            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                var type = _tree.Nodes[i].Type;
                _halves[i] = HalfOf(type);
                _priorities[i] = TreeMarkers.PriorityOf(type, _looks[i] == TreeNodeLook.Offered, IsTakenLook(_looks[i]));
            }
            _markers.Pick(_halves, _priorities, MarkerGapPx / MaxPixelsPerUnit);
            for (var i = 0; i < _tree.Nodes.Count; i++)
                if (_markers.IsShown(i))
                    _shownNodes.Add(_tree.Nodes[i]);
        }

        private static bool IsTakenLook(TreeNodeLook look) => look == TreeNodeLook.Taken || look == TreeNodeLook.OwnStart;

        private void PaintLinks(float pixelsPerUnit)
        {
            if (_tree == null || _linkMesh == null)
                return;
            var thin = WidthOf(LinkWorld, LinkPx, pixelsPerUnit);
            var thick = WidthOf(TakenLinkWorld, TakenLinkPx, pixelsPerUnit);
            for (var i = 0; i < _links.Count; i++)
            {
                var from = _looks[_links[i].From];
                var to = _looks[_links[i].To];
                var a = Position(_tree.Nodes[_links[i].From]);
                var b = Position(_tree.Nodes[_links[i].To]);
                var direction = (b - a).normalized;
                var side = new Vector2(-direction.y, direction.x) * ((IsTakenLook(from) && IsTakenLook(to) ? thick : thin) / 2f);
                _linkMesh.SetQuad(i, a - side, a + side, b + side, b - side, new Rect(0f, 0f, 1f, 1f));
                _linkMesh.SetColor(i, LinkColor(from, to));
            }
            _linkMesh.Apply();
        }

        private static float WidthOf(float world, float pixels, float pixelsPerUnit) => pixelsPerUnit > 0f ? Mathf.Max(world, pixels / pixelsPerUnit) : world;

        private void BuildGlyphs(PassiveTree tree)
        {
            _glyphLayers.Clear();
            var byGlyph = new Dictionary<string, List<int>>();
            for (var i = 0; i < tree.Nodes.Count; i++)
            {
                var glyph = TreeGlyph.Of(tree.Nodes[i]);
                if (glyph.Length == 0)
                    continue;
                if (!byGlyph.TryGetValue(glyph, out var nodes))
                    byGlyph[glyph] = nodes = new List<int>();
                nodes.Add(i);
            }
            foreach (var pair in byGlyph)
            {
                var sprite = UiIcons.Get(GlyphGroup, pair.Key);
                if (sprite == null)
                    continue;
                var texture = sprite.texture;
                var rect = sprite.textureRect;
                var uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
                var mesh = TreeMesh.Create("Glyph " + pair.Key, _map, texture, pair.Value.Count);
                _glyphLayers.Add(new GlyphLayer(mesh, pair.Value.ToArray(), uv));
            }
        }

        private void PaintGlyphs(float pixelsPerUnit)
        {
            if (_tree == null || _markers == null)
                return;
            foreach (var layer in _glyphLayers)
            {
                for (var slot = 0; slot < layer.Nodes.Length; slot++)
                {
                    var index = layer.Nodes[slot];
                    var half = (float)_halves[index];
                    var share = 2f * half * pixelsPerUnit < CompactNodePx ? CompactGlyphShare : GlyphShare;
                    layer.Mesh.SetSquare(slot, Position(_tree.Nodes[index]), half * share, layer.Uv);
                    layer.Mesh.SetColor(slot, _markers.IsShown(index) ? GlyphColorOf(_looks[index], _sectorColors[index]) : Hidden);
                }
                layer.Mesh.Apply();
            }
        }

        private float PixelsPerUnit() => _viewport == null ? 0f : _fieldPx.y / (float)_viewport.VisibleHeight;

        private static float HalfOf(TreeNodeType type) => Mathf.Max(Radius(type), MarkerPx(type) / 2f / MaxPixelsPerUnit);

        public static float MarkerPx(TreeNodeType type) => type switch
        {
            TreeNodeType.Start => 30f,
            TreeNodeType.Keystone => 26f,
            TreeNodeType.Notable => 20f,
            TreeNodeType.Jewel => 14f,
            _ => 16f
        };

        private void BuildRings(PassiveTree tree) => _ringMesh = TreeMesh.Create("Rings", _map, UiSprites.NodeAtlas, tree.Nodes.Count);

        private void BuildOutlines(PassiveTree tree)
        {
            _outlined.Clear();
            for (var i = 0; i < tree.Nodes.Count; i++)
                if (HasOutline(tree.Nodes[i].Type))
                    _outlined.Add(i);
            _outlineMesh = TreeMesh.Create("Outlines", _map, UiSprites.NodeAtlas, _outlined.Count);
        }

        private void PaintOutlines()
        {
            if (_outlineMesh == null || _tree == null || _markers == null)
                return;
            for (var slot = 0; slot < _outlined.Count; slot++)
            {
                var index = _outlined[slot];
                var node = _tree.Nodes[index];
                var look = _looks[index];
                _outlineMesh.SetSquare(slot, Position(node), (float)_halves[index] * OutlineScale, ShapeOf(node.Type, true));
                _outlineMesh.SetColor(slot, !_markers.IsShown(index) ? Hidden : node.Type == TreeNodeType.Keystone
                    ? look == TreeNodeLook.Locked ? Shade(TakenColor, LockedShade) : TakenColor
                    : WithAlpha(RingColorOf(look, _sectorColors[index]), NotableOuterAlpha));
            }
            _outlineMesh.Apply();
        }

        private static bool HasOutline(TreeNodeType type) => type == TreeNodeType.Notable || type == TreeNodeType.Keystone;

        private static Color32 SectorColor(TreeNodeDef node) => TreeGlyph.AttributeOf(node) switch
        {
            "red" => RedColor,
            "green" => GreenColor,
            "blue" => BlueColor,
            _ => WhiteColor
        };

        private static Color32 Shade(Color32 color, float share) =>
            new Color32((byte)(color.r * share), (byte)(color.g * share), (byte)(color.b * share), color.a);

        private static Color32 WithAlpha(Color32 color, byte alpha) => new Color32(color.r, color.g, color.b, alpha);

        private static Rect ShapeOf(TreeNodeType type, bool ring) => type switch
        {
            TreeNodeType.Mastery or TreeNodeType.Start => ring ? UiSprites.HexRingUv : UiSprites.HexUv,
            TreeNodeType.Keystone => ring ? UiSprites.DiamondRingUv : UiSprites.DiamondUv,
            _ => ring ? UiSprites.RingUv : UiSprites.DiscUv
        };

        private static Color32 RingColorOf(TreeNodeLook look, Color32 sector) => look switch
        {
            TreeNodeLook.Taken => TakenColor,
            TreeNodeLook.Offered => OfferedRing,
            TreeNodeLook.OwnStart => sector,
            TreeNodeLook.OtherStart => Shade(sector, OtherStartShade),
            TreeNodeLook.Locked => Shade(sector, LockedShade),
            _ => Shade(sector, NormalShade)
        };

        private static Color32 GlyphColorOf(TreeNodeLook look, Color32 sector) => look switch
        {
            TreeNodeLook.Taken => TakenColor,
            TreeNodeLook.Offered => Color32.Lerp(sector, OfferedRing, OfferedGlyphLift),
            TreeNodeLook.OwnStart => sector,
            TreeNodeLook.OtherStart => Shade(sector, OtherStartShade),
            TreeNodeLook.Locked => Shade(sector, LockedShade),
            _ => Color32.Lerp(sector, OfferedRing, NormalGlyphLift)
        };

        private readonly struct GlyphLayer
        {
            public readonly TreeMesh Mesh;
            public readonly int[] Nodes;
            public readonly Rect Uv;

            public GlyphLayer(TreeMesh mesh, int[] nodes, Rect uv)
            {
                Mesh = mesh;
                Nodes = nodes;
                Uv = uv;
            }
        }

        public static float Radius(TreeNodeType type) => type switch
        {
            TreeNodeType.Notable => 0.3f,
            TreeNodeType.Keystone => 0.5f,
            TreeNodeType.Mastery => 0.34f,
            TreeNodeType.Jewel => 0.3f,
            TreeNodeType.Start => 0.9f,
            _ => 0.2f
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
            _nodeMesh = TreeMesh.Create("Nodes", _map, UiSprites.NodeAtlas, tree.Nodes.Count);
            BuildRings(tree);
            BuildOutlines(tree);
            BuildGlyphs(tree);
            _selection = UiElements.Ring("Selection", _nodeMesh.rectTransform, SelectionColor).rectTransform;
            _selection.anchorMin = _selection.anchorMax = _map.pivot;
            _selection.gameObject.SetActive(false);
            _card.Root.SetAsLastSibling();
            _hint.SetAsLastSibling();
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
            var scale = PixelsPerUnit();
            _map.localScale = new Vector3(scale, scale, 1f);
            _map.anchoredPosition = new Vector2(-(float)_viewport.CentreX * scale, -(float)_viewport.CentreY * scale);
            if (!Mathf.Approximately(scale, _glyphScale))
            {
                _glyphScale = scale;
                Paint();
            }
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
            var pixelsPerUnit = PixelsPerUnit();
            var half = HalfOf(_selected.Type) * SelectionScale;
            if (pixelsPerUnit > 0f)
                half = Mathf.Max(half, HalfOf(_selected.Type) + SelectionGapPx / pixelsPerUnit);
            _selection.anchoredPosition = Position(_selected);
            _selection.sizeDelta = Vector2.one * (2f * half);
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
            if (_loop == null || _tree == null)
            {
                _pill.Text = string.Empty;
                ShowHint(TreeTexts.Unavailable);
                return;
            }
            var allocation = _loop.State.Tree;
            ShowPoints(allocation);
            if (_selected == null)
            {
                ShowHint(TreeTexts.Hint);
                return;
            }
            _actions.AddRange(TreeAction.For(_tree, allocation, _selected));
            if (_actions.Count > MaxActions)
                _actions.RemoveRange(MaxActions, _actions.Count - MaxActions);
            var card = TreeNodeCard.ForPassive(_tree, allocation, _selected, _loop.RefundCost(_selected.Id), NumberDisplay.Settings, _loop.Hero);
            _hint.gameObject.SetActive(false);
            _card.Show(IsCompact ? card.Shortened(CompactBodyLines) : card, _selected.Type, LookAt(_selected), _fieldPx.x - 2f * InsetPx, _actions, string.Empty);
            _card.Root.anchorMin = _card.Root.anchorMax = new Vector2(0.5f, 0f);
            _card.Root.pivot = new Vector2(0.5f, 0f);
            _card.Root.anchoredPosition = new Vector2(0f, InsetPx);
        }

        private void ShowHint(string text)
        {
            _card.Hide();
            _hint.gameObject.SetActive(true);
            _hintText.text = text;
        }

        private void ShowPoints(TreeAllocation allocation)
        {
            var gold = ColorUtility.ToHtmlStringRGB(_theme.Gold);
            _pill.Text = $"<color=#{gold}>{TreeTexts.Points(allocation.PointsLeft)}</color>  {TreeTexts.Taken(allocation.PointsSpent, _takeable)}";
            var width = _pill.Label!.GetPreferredValues(_pill.Label.text).x + 2f * PillPaddingPx;
            _pill.Rect.sizeDelta = new Vector2(width, PillHeightPx);
        }

        private RectTransform BuildHint(out TextMeshProUGUI text)
        {
            var back = UiElements.Fill("Hint", _field, HintColor, HintRadiusPx);
            var rect = UiElements.BottomEdge(back.rectTransform, HintHeightPx, InsetPx);
            rect.anchoredPosition = new Vector2(0f, InsetPx);
            text = UiElements.Label("HintText", rect, _theme.TextFont, 12f, StatusColor, TextAlignmentOptions.Center);
            UiElements.Stretch(text.rectTransform, 8f);
            text.overflowMode = TextOverflowModes.Ellipsis;
            return rect;
        }

        private UiButton Tool(string name, Vector2 anchor, Vector2 positionPx, Vector2 sizePx, Color color, float radiusPx, TMP_FontAsset? font = null, float fontSizePx = 0f)
        {
            var back = UiElements.Raise(UiElements.Fill(name, _field, color, radiusPx));
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
