namespace Game.Platform.Windows.Window
{
    public readonly struct ScreenRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public ScreenRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Width => Right - Left;
        public int Height => Bottom - Top;

        public bool Equals(ScreenRect other) =>
            Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    }
}
