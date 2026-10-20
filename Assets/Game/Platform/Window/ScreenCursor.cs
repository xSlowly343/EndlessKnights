namespace Game.Platform.Windows.Window
{
    public static class ScreenCursor
    {
        public static bool TryGetPosition(out int x, out int y)
        {
            var found = NativeMethods.GetCursorPos(out var point);
            x = point.X;
            y = point.Y;
            return found;
        }
    }
}
