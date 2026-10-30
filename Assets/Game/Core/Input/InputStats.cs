namespace Game.Core.Input
{
    public sealed class InputStats
    {
        public const double DefaultMetersPerPixel = 0.0254 / 96;

        public ulong Keys { get; set; }
        public ulong LeftClicks { get; set; }
        public ulong RightClicks { get; set; }
        public ulong MiddleClicks { get; set; }
        public double MouseMeters { get; set; }
        public double PlaySeconds { get; set; }
        public double IdleSeconds { get; set; }

        public ulong Clicks => LeftClicks + RightClicks + MiddleClicks;

        public void Add(in InputSnapshot snapshot, double metersPerPixel)
        {
            if (snapshot.Suppressed)
                return;
            Keys += (ulong)System.Math.Max(0, snapshot.KeyDowns);
            LeftClicks += (ulong)System.Math.Max(0, snapshot.LeftClicks);
            RightClicks += (ulong)System.Math.Max(0, snapshot.RightClicks);
            MiddleClicks += (ulong)System.Math.Max(0, snapshot.MiddleClicks);
            MouseMeters += System.Math.Max(0, snapshot.MouseDistancePx) * metersPerPixel;
        }
    }
}
