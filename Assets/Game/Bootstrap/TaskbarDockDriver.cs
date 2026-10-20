using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class TaskbarDockDriver : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;
        private const int GapToTaskbar = 0;

        private WindowController _window = null!;
        private TaskbarEdge _dockedEdge;
        private ScreenRect _dockedTaskbar;
        private bool _isDocked;
        private float _nextCheckTime;

        public static void Attach(GameObject host, WindowController window)
        {
            var driver = host.AddComponent<TaskbarDockDriver>();
            driver._window = window;
            driver.DockIfTaskbarMoved();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;
            DockIfTaskbarMoved();
        }

        private void DockIfTaskbarMoved()
        {
            if (!Taskbar.TryGetPosition(out var edge, out var taskbar))
                return;
            if (_isDocked && edge == _dockedEdge && taskbar.Equals(_dockedTaskbar))
                return;

            _window.PlaceAt(StripPlacement.NextToTaskbar(edge, taskbar, GameStartup.StripWidth, GameStartup.StripHeight, GapToTaskbar));
            _dockedEdge = edge;
            _dockedTaskbar = taskbar;
            _isDocked = true;
        }
    }
}
