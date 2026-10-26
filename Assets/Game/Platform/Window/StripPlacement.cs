using System;

namespace Game.Platform.Windows.Window
{
    public static class StripPlacement
    {
        public static bool IsHorizontal(TaskbarEdge edge) => edge == TaskbarEdge.Bottom || edge == TaskbarEdge.Top;

        public static int ClampOffset(TaskbarEdge edge, ScreenRect taskbar, int width, int height, int offset)
        {
            var room = IsHorizontal(edge) ? taskbar.Width - width : taskbar.Height - height;
            return Math.Max(0, Math.Min(offset, room));
        }

        public static int ClampDepth(TaskbarEdge edge, ScreenRect taskbar, int depth)
        {
            var thickness = IsHorizontal(edge) ? taskbar.Height : taskbar.Width;
            return Math.Max(0, Math.Min(depth, thickness));
        }

        public static int DepthDirection(TaskbarEdge edge) => edge == TaskbarEdge.Bottom || edge == TaskbarEdge.Right ? 1 : -1;

        public static ScreenRect NextToTaskbar(TaskbarEdge edge, ScreenRect taskbar, int width, int height, int gap, int offset, int depth = 0)
        {
            var along = ClampOffset(edge, taskbar, width, height, offset);
            var into = ClampDepth(edge, taskbar, depth);
            switch (edge)
            {
                case TaskbarEdge.Bottom:
                    return Sized(taskbar.Left + along, taskbar.Top - gap - height + into, width, height);
                case TaskbarEdge.Top:
                    return Sized(taskbar.Left + along, taskbar.Bottom + gap - into, width, height);
                case TaskbarEdge.Left:
                    return Sized(taskbar.Right + gap - into, taskbar.Top + along, width, height);
                case TaskbarEdge.Right:
                    return Sized(taskbar.Left - gap - width + into, taskbar.Top + along, width, height);
                default:
                    throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        private static ScreenRect Sized(int left, int top, int width, int height) =>
            new ScreenRect(left, top, left + width, top + height);
    }
}
