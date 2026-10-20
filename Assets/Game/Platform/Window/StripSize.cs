using System;

namespace Game.Platform.Windows.Window
{
    public readonly struct StripSize
    {
        public const int BaseDpi = 96;

        public readonly int Width;
        public readonly int Height;

        public StripSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public StripSize ScaledFor(uint dpi)
        {
            var scale = (dpi == 0 ? BaseDpi : dpi) / (double)BaseDpi;
            return new StripSize((int)Math.Round(Width * scale), (int)Math.Round(Height * scale));
        }
    }
}
