using System.Threading;
using Game.Core.Input;

namespace Game.Platform.Windows.Input
{
    public sealed class InputCounters
    {
        private int _keyDowns;

        public void AddKeyDown() => Interlocked.Increment(ref _keyDowns);

        public InputSnapshot Drain()
        {
            return new InputSnapshot(keyDowns: Interlocked.Exchange(ref _keyDowns, 0));
        }
    }
}
