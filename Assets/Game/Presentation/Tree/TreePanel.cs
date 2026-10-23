using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Tree
{
    public sealed class TreePanel : MonoBehaviour
    {
        public const float InfoLineHeightPx = 24f;
        public const float ButtonHeightPx = 30f;
        public const float MarginPx = 8f;
        public const int MaxActions = 4;
        public const int MaxInfoLines = 6;
        public const int CompactInfoLines = 2;
        public const float FullCardMinContentPx = 400f;
        private const int MaxLineLength = 50;
        private const float ClickSlopPx = 6f;
        private const float RefreshSeconds = 2f;
        private const double FirstViewHeight = 30;
        private const double PickRadiusShare = 0.03;
        private const double MinPickRadius = 0.6;
        private const float DefaultAspect = 1.8f;

        private static readonly Color LineColor = new Color(0.92f, 0.9f, 0.85f, 1f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.22f, 0.12f, 1f);

        private readonly List<TextMesh> _lines = new List<TextMesh>();
        private readonly List<StripButton> _buttons = new List<StripButton>();
        private readonly List<TextMesh> _buttonTexts = new List<TextMesh>();
        private readonly List<TreeAction> _actions = new List<TreeAction>();

        private Camera _mainCamera = null!;
        private TextMesh _hint = null!;
        private GameLoop? _loop;
        private PassiveTree? _tree;
        private TreeView? _view;
        private TreeViewport? _viewport;
        private TreeNodeDef? _selected;
        private TreeNodeLook[] _looks = System.Array.Empty<TreeNodeLook>();
        private Rect _treeArea;
        private float _pixelsPerUnit = 1f;
        private int _visibleLines = MaxInfoLines;
        private bool _pressedInTree;
        private Vector2 _pressPoint;
        private Vector2 _lastDragPoint;
        private bool _dragged;
        private float _sinceRefresh;

        public static TreePanel Create(Transform panel)
        {
            var panelTree = panel.gameObject.AddComponent<TreePanel>();
            panelTree.Build();
            return panelTree;
        }

        public void Bind(GameLoop loop, PassiveTree tree)
        {
            _loop = loop;
            _tree = tree;
            _view = TreeView.Create(tree, transform);
            _looks = new TreeNodeLook[tree.Nodes.Count];
            _viewport = new TreeViewport(tree.Nodes, _treeArea.height > 0f ? _treeArea.width / _treeArea.height : DefaultAspect);
            var start = tree.StartOf(loop.State.Tree.StartClass);
            if (start != null)
                _viewport.FocusOn(start.X, start.Y, FirstViewHeight);
            Refresh();
        }

        public Rect TreeArea => _treeArea;

        public Vector2 WorldPointOf(TreeNodeDef node)
        {
            if (_viewport == null)
                return _treeArea.center;
            var u = (node.X - _viewport.CentreX) / _viewport.VisibleWidth + 0.5;
            var v = (node.Y - _viewport.CentreY) / _viewport.VisibleHeight + 0.5;
            return new Vector2(_treeArea.xMin + (float)u * _treeArea.width, _treeArea.yMin + (float)v * _treeArea.height);
        }

        public void ZoomIn()
        {
            _viewport?.ZoomIn();
        }

        public void ZoomOut()
        {
            _viewport?.ZoomOut();
        }

        public void Arrange(float left, float bottom, float width, float height, float pixelsPerUnit)
        {
            _pixelsPerUnit = pixelsPerUnit;
            var margin = MarginPx / pixelsPerUnit;
            var lineHeight = InfoLineHeightPx / pixelsPerUnit;
            var buttonHeight = ButtonHeightPx / pixelsPerUnit;
            _visibleLines = height * pixelsPerUnit >= FullCardMinContentPx ? MaxInfoLines : CompactInfoLines;
            var cardHeight = _visibleLines * lineHeight + buttonHeight + 3 * margin;
            _treeArea = new Rect(left + margin, bottom + cardHeight, width - 2 * margin, height - cardHeight - margin);
            _viewport?.Resize(_treeArea.width / _treeArea.height);
            var firstLineY = _treeArea.yMin - margin - lineHeight / 2f;
            for (var i = 0; i < _lines.Count; i++)
            {
                _lines[i].transform.position = new Vector2(left + 2 * margin, firstLineY - i * lineHeight);
                _lines[i].gameObject.SetActive(i < _visibleLines);
            }
            _hint.transform.position = new Vector2(_treeArea.center.x, _treeArea.center.y);

            var buttonWidth = (width - 2 * margin - (MaxActions - 1) * margin) / MaxActions;
            var buttonY = bottom + margin + buttonHeight / 2f;
            for (var i = 0; i < MaxActions; i++)
            {
                var centre = new Vector2(left + margin + buttonWidth / 2f + i * (buttonWidth + margin), buttonY);
                _buttons[i].transform.position = new Vector3(centre.x, centre.y, _buttons[i].transform.position.z);
                _buttons[i].transform.localScale = new Vector3(buttonWidth, buttonHeight, 1f);
                _buttonTexts[i].transform.position = centre;
            }
        }

        public bool TryPress(Vector2 worldPoint)
        {
            for (var i = 0; i < _actions.Count; i++)
            {
                if (_buttons[i].Contains(worldPoint))
                {
                    Perform(_actions[i]);
                    return true;
                }
            }
            if (!_treeArea.Contains(worldPoint) || _viewport == null)
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
            if (!_dragged && (worldPoint - _pressPoint).magnitude * _pixelsPerUnit < ClickSlopPx)
                return;
            _dragged = true;
            var delta = worldPoint - _lastDragPoint;
            _viewport.Pan(delta.x / _treeArea.width, delta.y / _treeArea.height);
            _lastDragPoint = worldPoint;
        }

        public void CancelPress() => _pressedInTree = false;

        public void Release(Vector2 worldPoint)
        {
            if (!_pressedInTree)
                return;
            _pressedInTree = false;
            if (_dragged || _viewport == null || _tree == null)
                return;
            var u = (worldPoint.x - _treeArea.xMin) / _treeArea.width;
            var v = (worldPoint.y - _treeArea.yMin) / _treeArea.height;
            var (x, y) = _viewport.ToTree(u, v);
            var radius = System.Math.Max(MinPickRadius, _viewport.VisibleHeight * PickRadiusShare);
            _selected = TreeViewport.NodeAt(_tree.Nodes, x, y, radius);
            _view?.Select(_selected);
            ShowInfo();
        }

        private void Build()
        {
            _mainCamera = Camera.main!;
            for (var i = 0; i < MaxInfoLines; i++)
                _lines.Add(StripText.Create("TreeLine " + i, string.Empty, Vector2.zero, 0.17f, TextAnchor.MiddleLeft, LineColor, transform, 8));
            for (var i = 0; i < MaxActions; i++)
            {
                var button = StripButton.Create("TreeAction " + i, ButtonColor, Vector2.zero, Vector2.one, transform);
                button.GetComponent<SpriteRenderer>().sortingOrder = 6;
                _buttons.Add(button);
                _buttonTexts.Add(StripText.Create("TreeActionText " + i, string.Empty, Vector2.zero, 0.16f, TextAnchor.MiddleCenter, Color.white, transform, 8));
            }
            _hint = StripText.Create("TreeHint", "Дерево недоступно", Vector2.zero, 0.16f, TextAnchor.MiddleCenter, LineColor, transform, 8);
            ShowActions();
        }

        private void LateUpdate()
        {
            _hint.gameObject.SetActive(_view == null);
            if (_view == null || _viewport == null)
                return;
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= RefreshSeconds)
                Refresh();
            _view.ShowViewport(_viewport, ScreenRect(_treeArea));
        }

        private Rect ScreenRect(Rect world)
        {
            var min = _mainCamera.WorldToViewportPoint(world.min);
            var max = _mainCamera.WorldToViewportPoint(world.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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

        private void Refresh()
        {
            _sinceRefresh = 0f;
            if (_loop == null || _tree == null || _view == null)
                return;
            var allocation = _loop.State.Tree;
            var offered = new HashSet<TreeNodeDef>(allocation.Allocatable(_tree));
            var canTake = allocation.PointsLeft > 0;
            for (var i = 0; i < _tree.Nodes.Count; i++)
                _looks[i] = LookOf(_tree.Nodes[i], allocation, offered.Contains(_tree.Nodes[i]) && canTake);
            _view.Recolor(_looks);
            ShowInfo();
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
                ShowActions();
                return;
            }
            var allocation = _loop.State.Tree;
            var info = new List<string> { $"Свободных очков: {allocation.PointsLeft} · Золото: {NumberFormatter.Format(_loop.State.Gold, NumberFormatSettings.Default)}" };
            if (_selected == null)
                info.Add("Нажми на узел, тяни, чтобы двигать дерево");
            else
            {
                info.AddRange(TreeNodeText.Describe(_tree, allocation, _selected, _loop.RefundCost(_selected.Id), NumberFormatSettings.Default));
                _actions.AddRange(TreeAction.For(_tree, allocation, _selected));
                if (_actions.Count > MaxActions)
                    _actions.RemoveRange(MaxActions, _actions.Count - MaxActions);
            }
            for (var i = 0; i < _lines.Count; i++)
                _lines[i].text = i < info.Count ? Shorten(info[i]) : string.Empty;
            ShowActions();
        }

        private void ShowActions()
        {
            for (var i = 0; i < MaxActions; i++)
            {
                var visible = i < _actions.Count;
                _buttons[i].gameObject.SetActive(visible);
                _buttonTexts[i].gameObject.SetActive(visible);
                if (visible)
                    _buttonTexts[i].text = _actions[i].Label;
            }
        }

        private static string Shorten(string line) => line.Length <= MaxLineLength ? line : line.Substring(0, MaxLineLength - 1) + "…";
    }
}
