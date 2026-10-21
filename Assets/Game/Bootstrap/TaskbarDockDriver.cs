using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class TaskbarDockDriver : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;
        private const int GapToTaskbar = 0;
        private const int DefaultOffsetAlongTaskbar = 0;

        private static readonly StripSize BaseStripSize = new StripSize(GameStartup.StripWidth, GameStartup.StripHeight);

        private readonly StripDrag _drag = new StripDrag();
        private WindowController _window = null!;
        private TaskbarEdge _dockedEdge;
        private ScreenRect _dockedTaskbar;
        private uint _dockedDpi;
        private StripSize _size = BaseStripSize;
        private bool _isDocked;
        private int _offset = DefaultOffsetAlongTaskbar;
        private float _nextCheckTime;

        public static TaskbarDockDriver Attach(GameObject host, WindowController window)
        {
            var driver = host.AddComponent<TaskbarDockDriver>();
            driver._window = window;
            driver.DockIfTaskbarChanged();
            return driver;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;
            DockIfTaskbarChanged();
        }

        public void BeginDrag()
        {
            if (_isDocked && TryGetCursorAlongTaskbar(out var pressedAt))
                _drag.Press(pressedAt, _offset);
        }

        public void ContinueDrag()
        {
            if (_isDocked && TryGetCursorAlongTaskbar(out var cursor) && _drag.TryMove(cursor, out var offset))
                MoveAlongTaskbar(offset);
        }

        public void EndDrag() => _drag.Release();

        private bool TryGetCursorAlongTaskbar(out int position)
        {
            position = 0;
            if (!ScreenCursor.TryGetPosition(out var x, out var y))
                return false;
            position = StripPlacement.IsHorizontal(_dockedEdge) ? x : y;
            return true;
        }

        private void MoveAlongTaskbar(int offset)
        {
            _offset = StripPlacement.ClampOffset(_dockedEdge, _dockedTaskbar, _size.Width, _size.Height, offset);
            PlaceNextToTaskbar();
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

        private void PlaceNextToTaskbar()
        {
            _window.PlaceAt(StripPlacement.NextToTaskbar(_dockedEdge, _dockedTaskbar, _size.Width, _size.Height, GapToTaskbar, _offset));
        }
    }
}
