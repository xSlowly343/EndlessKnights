namespace Game.Platform.Windows.Input
{
    public sealed class KeyboardEventFilter
    {
        public const ushort BreakFlag = 0x0001;

        private const int KeyCount = 256;
        private const int BitsPerWord = 64;
        private const ushort Backspace = 0x08;
        private const ushort Enter = 0x0D;
        private const ushort Shift = 0x10;
        private const ushort Control = 0x11;
        private const ushort Alt = 0x12;
        private const ushort Space = 0x20;
        private const ushort LeftWindows = 0x5B;
        private const ushort RightWindows = 0x5C;
        private const ushort LeftShift = 0xA0;
        private const ushort RightAlt = 0xA5;
        private const ushort InvalidKey = 0xFF;

        private readonly ulong[] _held = new ulong[KeyCount / BitsPerWord];

        public static bool IsKeyDown(ushort flags) => (flags & BreakFlag) == 0;

        public KeyClass Process(ushort virtualKey, ushort flags)
        {
            if (virtualKey >= InvalidKey)
                return KeyClass.None;

            var word = virtualKey / BitsPerWord;
            var bit = 1UL << (virtualKey % BitsPerWord);

            if (!IsKeyDown(flags))
            {
                _held[word] &= ~bit;
                return KeyClass.None;
            }

            if ((_held[word] & bit) != 0)
                return KeyClass.None;
            _held[word] |= bit;

            return IsModifier(virtualKey) ? KeyClass.None : Classify(virtualKey);
        }

        private static bool IsModifier(ushort virtualKey) =>
            virtualKey == Shift || virtualKey == Control || virtualKey == Alt
            || virtualKey == LeftWindows || virtualKey == RightWindows
            || (virtualKey >= LeftShift && virtualKey <= RightAlt);

        private static KeyClass Classify(ushort virtualKey)
        {
            switch (virtualKey)
            {
                case Enter:
                    return KeyClass.Enter;
                case Backspace:
                    return KeyClass.Backspace;
                case Space:
                    return KeyClass.Space;
                default:
                    return KeyClass.Regular;
            }
        }
    }
}
