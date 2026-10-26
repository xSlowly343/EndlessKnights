using System.Threading;
using Game.Core.Input;

namespace Game.Platform.Windows.Input
{
    public sealed class InputCounters
    {
        private const double MilliPerPx = 1000.0;
        private const double MilliPerTurn = 1000.0;

        private int _keyDowns;
        private int _enterDowns;
        private int _backspaceDowns;
        private int _spaceDowns;
        private int _comboDowns;
        private int _reversals;
        private int _leftClicks;
        private int _rightClicks;
        private int _middleClicks;
        private int _wheelTicks;
        private int _flicks;
        private long _circleMilliTurns;
        private long _mouseDistanceMilliPx;
        private long _lastInputMilliseconds = long.MinValue;
        private int _rhythmCvBits;

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
            else if (keyClass == KeyClass.Combo)
                Interlocked.Increment(ref _comboDowns);
        }

        public void AddClicks(MouseButtons buttons)
        {
            if (buttons.Left > 0)
                Interlocked.Add(ref _leftClicks, buttons.Left);
            if (buttons.Right > 0)
                Interlocked.Add(ref _rightClicks, buttons.Right);
            if (buttons.Middle > 0)
                Interlocked.Add(ref _middleClicks, buttons.Middle);
        }

        public void AddWheelTicks(int ticks)
        {
            if (ticks > 0)
                Interlocked.Add(ref _wheelTicks, ticks);
        }

        public void AddMouseDistance(double distancePx)
        {
            if (distancePx > 0)
                Interlocked.Add(ref _mouseDistanceMilliPx, (long)(distancePx * MilliPerPx));
        }

        public void AddFlick() => Interlocked.Increment(ref _flicks);

        public void AddReversals(int reversals)
        {
            if (reversals > 0)
                Interlocked.Add(ref _reversals, reversals);
        }

        public void AddCircleTurns(int turns)
        {
            if (turns > 0)
                Interlocked.Add(ref _circleMilliTurns, (long)(turns * MilliPerTurn));
        }

        public void MarkInput(long nowMilliseconds) => Interlocked.Exchange(ref _lastInputMilliseconds, nowMilliseconds);

        public void SetRhythm(float coefficientOfVariation) =>
            Interlocked.Exchange(ref _rhythmCvBits, System.BitConverter.SingleToInt32Bits(coefficientOfVariation));

        public InputSnapshot Drain(long nowMilliseconds = 0, bool suppressed = false)
        {
            var lastInput = Interlocked.Read(ref _lastInputMilliseconds);
            var secondsSinceLastInput = lastInput == long.MinValue ? 0d : (nowMilliseconds - lastInput) / 1000d;
            return new InputSnapshot(
                keyDowns: Interlocked.Exchange(ref _keyDowns, 0),
                enterDowns: Interlocked.Exchange(ref _enterDowns, 0),
                backspaceDowns: Interlocked.Exchange(ref _backspaceDowns, 0),
                spaceDowns: Interlocked.Exchange(ref _spaceDowns, 0),
                leftClicks: Interlocked.Exchange(ref _leftClicks, 0),
                rightClicks: Interlocked.Exchange(ref _rightClicks, 0),
                middleClicks: Interlocked.Exchange(ref _middleClicks, 0),
                wheelTicks: Interlocked.Exchange(ref _wheelTicks, 0),
                mouseDistancePx: (float)(Interlocked.Exchange(ref _mouseDistanceMilliPx, 0L) / MilliPerPx),
                flicks: Interlocked.Exchange(ref _flicks, 0),
                circleTurns: (float)(Interlocked.Exchange(ref _circleMilliTurns, 0L) / MilliPerTurn),
                secondsSinceLastInput: secondsSinceLastInput < 0 ? 0d : secondsSinceLastInput,
                rhythmCv: System.BitConverter.Int32BitsToSingle(Volatile.Read(ref _rhythmCvBits)),
                suppressed: suppressed,
                comboDowns: Interlocked.Exchange(ref _comboDowns, 0),
                reversals: Interlocked.Exchange(ref _reversals, 0));
        }
    }
}
