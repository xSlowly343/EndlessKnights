using System.Threading;
using Game.Core.Input;

namespace Game.Platform.Windows.Input
{
    public sealed class InputCounters
    {
        private int _keyDowns;
        private int _enterDowns;
        private int _backspaceDowns;
        private int _spaceDowns;
        private long _mouseDistanceMilliPx;

        private const double MilliPerPx = 1000.0;

        public void AddKey(KeyClass keyClass)
        {
            if (keyClass == KeyClass.None)
                return;

            Interlocked.Increment(ref _keyDowns);
            if (keyClass == KeyClass.Enter)
                Interlocked.Increment(ref _enterDowns);
            else if (keyClass == KeyClass.Backspace)
                Interlocked.Increment(ref _backspaceDowns);
            else if (keyClass == KeyClass.Space)
                Interlocked.Increment(ref _spaceDowns);
        }

        public void AddMouseDistance(double distancePx)
        {
            if (distancePx > 0)
                Interlocked.Add(ref _mouseDistanceMilliPx, (long)(distancePx * MilliPerPx));
        }

        public InputSnapshot Drain()
        {
            return new InputSnapshot(
                keyDowns: Interlocked.Exchange(ref _keyDowns, 0),
                enterDowns: Interlocked.Exchange(ref _enterDowns, 0),
                backspaceDowns: Interlocked.Exchange(ref _backspaceDowns, 0),
                spaceDowns: Interlocked.Exchange(ref _spaceDowns, 0),
                mouseDistancePx: (float)(Interlocked.Exchange(ref _mouseDistanceMilliPx, 0L) / MilliPerPx));
        }
    }
}
