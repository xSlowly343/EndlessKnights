using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Sim.Rift
{
    public sealed class RiftState
    {
        public long WindowId { get; set; }
        public long WindowEndUnixMs { get; set; }
        public bool Inside { get; set; }
        public StageProgress? OtherWorld { get; set; }
        public BigNumber Shards { get; set; }
        public long Deepest { get; set; } = 1;
        public string Supply { get; set; } = string.Empty;
        public HashSet<string> Opened { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public long SupplyWindow { get; set; }
        public Dictionary<string, int> Artifacts { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);

        public void StartWindow(long windowId, long endUnixMs)
        {
            WindowId = windowId;
            WindowEndUnixMs = endUnixMs;
            OtherWorld = null;
            Shards = BigNumber.Zero;
            Deepest = 1;
            Artifacts.Clear();
            Opened.Clear();
        }

        public void EndWindow()
        {
            OtherWorld = null;
            Shards = BigNumber.Zero;
            Artifacts.Clear();
        }
    }
}
