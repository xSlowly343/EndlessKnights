using System;
using System.Collections;
using Game.Core.Ui;
using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class TaskbarDockDriver : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;
        private const float RaiseIntervalSeconds = 0.25f;
        private const int GapToTaskbar = 0;
        private const int DefaultOffsetAlongTaskbar = 0;

        private static readonly StripSize BaseStripSize = new StripSize(GameStartup.StripWidth, GameStartup.StripHeight);

        private readonly StripDrag _drag = new StripDrag();
        private readonly StripDrag _depthDrag = new StripDrag();
        private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();
        private WindowController _window = null!;
        private TaskbarEdge _dockedEdge;
        private ScreenRect _dockedTaskbar;
        private uint _dockedDpi;
        private StripSize _size = BaseStripSize;
        private bool _isDocked;
        private int _offset = DefaultOffsetAlongTaskbar;
        private int _depth;
        private float _nextRaiseTime;
        private float _nextCheckTime;
        private bool _expanded;
        private ScreenRect _pendingWindow;
        private bool _hasPendingWindow;

        public event Action<PanelLayout, int>? Placed;
        public event Action<int>? Moved;

        public PanelLayout Layout { get; private set; }
        public bool AlwaysOnTop { get; set; } = true;
        public int StripHeightPx => _size.Height;
        public int StripWidthPx => _size.Width;
        public int Offset => _offset;

        public void UseOffset(int offset)
        {
            _offset = _isDocked ? StripPlacement.ClampOffset(_dockedEdge, _dockedTaskbar, _size.Width, _size.Height, offset) : offset;
            if (_isDocked)
                PlaceNextToTaskbar();
        }

        public static TaskbarDockDriver Attach(GameObject host, WindowController window)
        {
            var driver = host.AddComponent<TaskbarDockDriver>();
            driver._window = window;
            driver.StartCoroutine(driver.MoveWindowAfterRender());
            driver.DockIfTaskbarChanged();
            return driver;
        }

        private IEnumerator MoveWindowAfterRender()
        {
            while (true)
            {
                yield return _endOfFrame;
                if (!_hasPendingWindow)
                    continue;
                _hasPendingWindow = false;
                _window.PlaceAt(_pendingWindow);
            }
        }

        private void Update()
        {
            RaiseOverTaskbar();
            if (Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;
            DockIfTaskbarChanged();
            RestoreIfResized();
        }

        private void RestoreIfResized()
        {
            if (_isDocked && _window.TryGetBounds(out var bounds) && !bounds.Equals(Layout.Window))
                PlaceNextToTaskbar();
        }

        public void Expand()
        {
            _expanded = true;
            _drag.Release();
            _depthDrag.Release();
        }

        public void Collapse() => _expanded = false;

        public void BeginDrag()
        {
            if (!_isDocked || _expanded || !TryGetCursor(out var along, out var across))
                return;
            _drag.Press(along, _offset);
            _depthDrag.Press(across * StripPlacement.DepthDirection(_dockedEdge), _depth);
        }

        public void ContinueDrag()
        {
            if (!_isDocked || !TryGetCursor(out var along, out var across))
                return;
            var moved = false;
            if (_drag.TryMove(along, out var offset))
            {
                _offset = StripPlacement.ClampOffset(_dockedEdge, _dockedTaskbar, _size.Width, _size.Height, offset);
                moved = true;
            }
            if (_depthDrag.TryMove(across * StripPlacement.DepthDirection(_dockedEdge), out var depth))
            {
                _depth = StripPlacement.ClampDepth(_dockedEdge, _dockedTaskbar, depth);
                moved = true;
            }
            if (moved)
                PlaceNextToTaskbar();
        }

        public void EndDrag()
        {
            var dragging = _drag.IsDragging;
            _drag.Release();
            _depthDrag.Release();
            if (dragging)
                Moved?.Invoke(_offset);
        }

        private bool TryGetCursor(out int along, out int across)
        {
            along = across = 0;
            if (!ScreenCursor.TryGetPosition(out var x, out var y))
                return false;
            var horizontal = StripPlacement.IsHorizontal(_dockedEdge);
            along = horizontal ? x : y;
            across = horizontal ? y : x;
            return true;
        }

        private void RaiseOverTaskbar()
        {
            if (_depth <= 0 || !AlwaysOnTop || Time.unscaledTime < _nextRaiseTime)
                return;
            _nextRaiseTime = Time.unscaledTime + RaiseIntervalSeconds;
            _window.SetAlwaysOnTop(true);
        }

        public void ForceRedock()
        {
            _isDocked = false;
            DockIfTaskbarChanged();
        }

        private void DockIfTaskbarChanged()
        {
            if (!Taskbar.TryGetPosition(out var edge, out var taskbar))
                return;
            var dpi = MonitorDpi.ForRect(taskbar);
            if (_isDocked && edge == _dockedEdge && taskbar.Equals(_dockedTaskbar) && dpi == _dockedDpi)
                return;

            _dockedEdge = edge;
            _dockedTaskbar = taskbar;
            _dockedDpi = dpi;
            _size = BaseStripSize.ScaledFor(dpi);
            _isDocked = true;
            PlaceNextToTaskbar();
        }

        private int PanelAreaPx(int basePx) => (int)Math.Round((double)basePx * _size.Height / BaseStripSize.Height);

        private void PlaceNextToTaskbar()
        {
            var strip = StripPlacement.NextToTaskbar(_dockedEdge, _dockedTaskbar, _size.Width, _size.Height, GapToTaskbar, _offset, _depth);
            Layout = MonitorWorkArea.TryGet(strip, out var workArea)
                ? PanelLayout.Expanded(strip, workArea, _dockedEdge, PanelAreaPx(PanelGeometry.WideWidthPx), PanelAreaPx(PanelGeometry.WideHeightPx + PanelGeometry.GapToStripPx))
                : PanelLayout.Collapsed(strip);
            _pendingWindow = Layout.Window;
            _hasPendingWindow = true;
            Placed?.Invoke(Layout, _size.Height);
        }
    }
}
