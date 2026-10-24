namespace Game.Core.Content.Rift
{
    public readonly struct RiftWindow
    {
        public RiftWindow(long startUnixMs, long endUnixMs)
        {
            StartUnixMs = startUnixMs;
            EndUnixMs = endUnixMs;
        }

        public long StartUnixMs { get; }
        public long EndUnixMs { get; }
        public long Id => StartUnixMs / 1000;
        public bool Contains(long unixMs) => unixMs >= StartUnixMs && unixMs < EndUnixMs;
    }
}
