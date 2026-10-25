namespace Game.Platform.Windows.Window
{
    public sealed class SurfaceResizeGate
    {
        public const int MaxWaitFrames = 6;

        private PanelLayout _pending;
        private int _waitedFrames;

        public bool HasPending { get; private set; }

        public void Request(PanelLayout layout)
        {
            _pending = layout;
            _waitedFrames = 0;
            HasPending = true;
        }

        public bool TryTake(int surfaceWidthPx, int surfaceHeightPx, out PanelLayout layout)
        {
            layout = _pending;
            if (!HasPending)
                return false;
            if (!Fits(surfaceWidthPx, surfaceHeightPx) && ++_waitedFrames < MaxWaitFrames)
                return false;
            HasPending = false;
            return true;
        }

        private bool Fits(int surfaceWidthPx, int surfaceHeightPx) =>
            _pending.Window.Width == surfaceWidthPx && _pending.Window.Height == surfaceHeightPx;
    }
}
