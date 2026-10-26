using System;

namespace Game.Platform.Windows.Window
{
    public readonly struct PanelLayout
    {
        public readonly ScreenRect Window;
        public readonly int StripOffsetX;
        public readonly int StripOffsetFromBottom;
        public readonly bool PanelsAboveStrip;

        private PanelLayout(ScreenRect window, int stripOffsetX, int stripOffsetFromBottom, bool panelsAboveStrip)
        {
            Window = window;
            StripOffsetX = stripOffsetX;
            StripOffsetFromBottom = stripOffsetFromBottom;
            PanelsAboveStrip = panelsAboveStrip;
        }

        public static PanelLayout Collapsed(ScreenRect strip) => new PanelLayout(strip, 0, 0, true);

        public static PanelLayout Expanded(ScreenRect strip, ScreenRect workArea, TaskbarEdge edge, int panelWidth, int panelHeight)
        {
            var width = Math.Min(Math.Max(strip.Width, panelWidth), workArea.Width);
            var left = Math.Max(workArea.Left, Math.Min(PreferredLeft(strip, workArea, width), workArea.Right - width));

            if (edge == TaskbarEdge.Top)
            {
                var height = Math.Min(strip.Height + panelHeight, workArea.Bottom - strip.Top);
                var window = new ScreenRect(left, strip.Top, left + width, strip.Top + height);
                return new PanelLayout(window, strip.Left - left, height - strip.Height, false);
            }

            var grownHeight = Math.Min(strip.Height + panelHeight, strip.Bottom - workArea.Top);
            var grown = new ScreenRect(left, strip.Bottom - grownHeight, left + width, strip.Bottom);
            return new PanelLayout(grown, strip.Left - left, 0, true);
        }

        public int PanelLeft(int panelWidth, int stripWidth)
        {
            var preferred = StripOffsetX > 0 ? stripWidth - panelWidth : 0;
            var lowest = -StripOffsetX;
            var highest = Window.Width - StripOffsetX - panelWidth;
            return Math.Max(lowest, Math.Min(preferred, highest));
        }

        private static int PreferredLeft(ScreenRect strip, ScreenRect workArea, int width)
        {
            var roomToTheLeft = strip.Right - workArea.Left;
            var roomToTheRight = workArea.Right - strip.Left;
            return roomToTheLeft >= roomToTheRight ? strip.Right - width : strip.Left;
        }
    }
}
