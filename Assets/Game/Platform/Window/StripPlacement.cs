using System;

namespace Game.Platform.Windows.Window
{
    public static class StripPlacement
    {
        public static ScreenRect NextToTaskbar(TaskbarEdge edge, ScreenRect taskbar, int width, int height, int gap)
        {
            switch (edge)
            {
                case TaskbarEdge.Bottom:
                    return Sized(taskbar.Right - width, taskbar.Top - gap - height, width, height);
                case TaskbarEdge.Top:
                    return Sized(taskbar.Right - width, taskbar.Bottom + gap, width, height);
                case TaskbarEdge.Left:
                    return Sized(taskbar.Right + gap, taskbar.Bottom - height, width, height);
                case TaskbarEdge.Right:
                    return Sized(taskbar.Left - gap - width, taskbar.Bottom - height, width, height);
                default:
                    throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        private static ScreenRect Sized(int left, int top, int width, int height) =>
            new ScreenRect(left, top, left + width, top + height);
    }
}
