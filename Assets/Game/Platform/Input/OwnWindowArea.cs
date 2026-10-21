using System.Threading;
using Game.Platform.Windows.Window;

namespace Game.Platform.Windows.Input
{
    public sealed class OwnWindowArea
    {
        private ScreenRect _bounds;
        private int _hasBounds;

        public void Set(ScreenRect bounds)
        {
            _bounds = bounds;
            Interlocked.Exchange(ref _hasBounds, 1);
        }

        public bool Contains(int x, int y)
        {
            if (Volatile.Read(ref _hasBounds) == 0)
                return false;
            var bounds = _bounds;
            return x >= bounds.Left && x < bounds.Right && y >= bounds.Top && y < bounds.Bottom;
        }
    }
}
