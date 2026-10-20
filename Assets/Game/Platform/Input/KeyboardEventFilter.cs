namespace Game.Platform.Windows.Input
{
    public static class KeyboardEventFilter
    {
        public const ushort BreakFlag = 0x0001;

        public static bool IsKeyDown(ushort flags) => (flags & BreakFlag) == 0;
    }
}
