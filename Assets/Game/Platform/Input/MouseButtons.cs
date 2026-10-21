namespace Game.Platform.Windows.Input
{
    public readonly struct MouseButtons
    {
        private const ushort LeftDown = 0x0001;
        private const ushort RightDown = 0x0004;
        private const ushort MiddleDown = 0x0010;
        private const ushort Wheel = 0x0400;

        public readonly int Left;
        public readonly int Right;
        public readonly int Middle;
        public readonly int WheelDelta;

        private MouseButtons(int left, int right, int middle, int wheelDelta)
        {
            Left = left;
            Right = right;
            Middle = middle;
            WheelDelta = wheelDelta;
        }

        public bool AnyClick => Left + Right + Middle > 0;

        public static MouseButtons Decode(ushort buttonFlags, short buttonData) => new MouseButtons(
            (buttonFlags & LeftDown) != 0 ? 1 : 0,
            (buttonFlags & RightDown) != 0 ? 1 : 0,
            (buttonFlags & MiddleDown) != 0 ? 1 : 0,
            (buttonFlags & Wheel) != 0 ? buttonData : 0);
    }
}
