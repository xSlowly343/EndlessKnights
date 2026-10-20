using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    public sealed class TaskbarDockDriver : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;
        private const int GapToTaskbar = 0;
        private const int DefaultOffsetAlongTaskbar = 0;

        private readonly StripDrag _drag = new StripDrag();
        private WindowController _window = null!;
        private FocusModeButton _focusButton = null!;
        private TaskbarEdge _dockedEdge;
        private ScreenRect _dockedTaskbar;
        private bool _isDocked;
        private int _offset = DefaultOffsetAlongTaskbar;
        private float _nextCheckTime;

        public static void Attach(FocusModeButton focusButton, WindowController window)
        {
            var driver = focusButton.gameObject.AddComponent<TaskbarDockDriver>();
            driver._window = window;
            driver._focusButton = focusButton;
            driver.DockIfTaskbarMoved();
        }

        private void Update()
        {
            HandleDrag();

            if (Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;
            DockIfTaskbarMoved();
        }

        private void HandleDrag()
        {
            var mouse = Mouse.current;
            if (mouse == null || !_isDocked)
                return;

            if (mouse.leftButton.wasPressedThisFrame && !_focusButton.IsUnderPointer() && TryGetCursorAlongTaskbar(out var pressedAt))
                _drag.Press(pressedAt, _offset);
            else if (mouse.leftButton.wasReleasedThisFrame)
                _drag.Release();
            else if (mouse.leftButton.isPressed && TryGetCursorAlongTaskbar(out var cursor) && _drag.TryMove(cursor, out var offset))
                MoveAlongTaskbar(offset);
        }

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
            _offset = StripPlacement.ClampOffset(_dockedEdge, _dockedTaskbar, GameStartup.StripWidth, GameStartup.StripHeight, offset);
            PlaceNextToTaskbar();
        }

        private void DockIfTaskbarMoved()
        {
            if (!Taskbar.TryGetPosition(out var edge, out var taskbar))
                return;
            if (_isDocked && edge == _dockedEdge && taskbar.Equals(_dockedTaskbar))
                return;

            _dockedEdge = edge;
            _dockedTaskbar = taskbar;
            _isDocked = true;
            PlaceNextToTaskbar();
        }

        private void PlaceNextToTaskbar()
        {
            _window.PlaceAt(StripPlacement.NextToTaskbar(_dockedEdge, _dockedTaskbar, GameStartup.StripWidth, GameStartup.StripHeight, GapToTaskbar, _offset));
        }
    }
}
