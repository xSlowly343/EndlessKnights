using System;
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
        private int _panelWidthPx;
        private int _panelHeightPx;

        public event Action<PanelLayout, int>? Placed;
        public event Action<int>? Moved;

        public PanelLayout Layout { get; private set; }
        public bool AlwaysOnTop { get; set; } = true;
        public int StripHeightPx => _size.Height;
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
            driver.DockIfTaskbarChanged();
            return driver;
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

        public PanelLayout Expand(int panelWidthPx, int panelHeightPx)
        {
            _expanded = true;
            _panelWidthPx = panelWidthPx;
            _panelHeightPx = panelHeightPx;
            _drag.Release();
            _depthDrag.Release();
            PlaceNextToTaskbar();
            return Layout;
        }

        public void Collapse()
        {
            _expanded = false;
            PlaceNextToTaskbar();
        }

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

        private int PlacedDepth => _expanded && !StripPlacement.IsHorizontal(_dockedEdge) ? 0 : _depth;

        private void PlaceNextToTaskbar()
        {
            var strip = StripPlacement.NextToTaskbar(_dockedEdge, _dockedTaskbar, _size.Width, _size.Height, GapToTaskbar, _offset, PlacedDepth);
            Layout = _expanded && MonitorWorkArea.TryGet(strip, out var workArea)
                ? PanelLayout.Expanded(strip, workArea, _dockedEdge, _panelWidthPx, _panelHeightPx)
                : PanelLayout.Collapsed(strip);
            _window.PlaceAt(Layout.Window);
            Placed?.Invoke(Layout, _size.Height);
        }
    }
}
