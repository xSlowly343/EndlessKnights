using System;

namespace Game.Platform.Windows.Window
{
    public sealed class StripDrag
    {
        public const int StartThresholdPixels = 4;

        private bool _pressed;
        private int _startCursor;
        private int _startOffset;

        public bool IsDragging { get; private set; }

        public void Press(int cursor, int currentOffset)
        {
            _pressed = true;
            IsDragging = false;
            _startCursor = cursor;
            _startOffset = currentOffset;
        }

        public bool TryMove(int cursor, out int offset)
        {
            offset = _startOffset;
            if (!_pressed)
                return false;

            var delta = cursor - _startCursor;
            if (!IsDragging && Math.Abs(delta) < StartThresholdPixels)
                return false;

            IsDragging = true;
            offset = _startOffset + delta;
            return true;
        }

        public void Release()
        {
            _pressed = false;
            IsDragging = false;
        }
    }
}
