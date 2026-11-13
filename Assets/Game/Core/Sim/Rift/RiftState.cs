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
        public GameState? OtherHero { get; set; }
        public BigNumber Shards { get; set; }
        public long Sparks { get; set; }
        public long ShopCredited { get; set; }
        public long Deepest { get; set; } = 1;
        public string Supply { get; set; } = string.Empty;
        public HashSet<string> Opened { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public long SupplyWindow { get; set; }
        public Dictionary<string, int> Artifacts { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public string WindowSet { get; set; } = string.Empty;
        public List<string> Gifts { get; } = new List<string>();
        public List<string> GiftOffer { get; } = new List<string>();
        public int GiftOffers { get; set; }

        public void StartWindow(long windowId, long endUnixMs)
        {
            WindowId = windowId;
            WindowEndUnixMs = endUnixMs;
            OtherWorld = null;
            OtherHero = null;
            Shards = BigNumber.Zero;
            Sparks = 0;
            ShopCredited = 0;
            Deepest = 1;
            Artifacts.Clear();
            Opened.Clear();
            ClearRun();
        }

        private void ClearRun()
        {
            WindowSet = string.Empty;
            Gifts.Clear();
            GiftOffer.Clear();
            GiftOffers = 0;
        }

        public void EndWindow()
        {
            OtherWorld = null;
            OtherHero = null;
            Shards = BigNumber.Zero;
            Sparks = 0;
            ShopCredited = 0;
            Artifacts.Clear();
            ClearRun();
        }
    }
}
