namespace Game.Platform.Windows.Window
{
    public static class PointerState
    {
        private const int LeftButton = 0x01;
        private const int RightButton = 0x02;
        private const int SwapButtonMetric = 23;
        private const int DownBit = 0x8000;
        private const int PressedSinceLastPollBit = 0x0001;

        public static (bool Down, bool PressedSinceLastPoll) ReadPrimaryButton()
        {
            var primary = NativeMethods.GetSystemMetrics(SwapButtonMetric) != 0 ? RightButton : LeftButton;
            var state = NativeMethods.GetAsyncKeyState(primary);
            return ((state & DownBit) != 0, (state & PressedSinceLastPollBit) != 0);
        }
    }
}
