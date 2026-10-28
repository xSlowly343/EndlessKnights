using System.Collections.Generic;
using Game.Core.Content.Items;

namespace Game.Core.Sim
{
    public sealed class JewelState
    {
        public List<JewelItem> Bag { get; } = new List<JewelItem>();
        public Dictionary<string, JewelItem> Sockets { get; } = new Dictionary<string, JewelItem>(System.StringComparer.Ordinal);
        public long Found { get; set; }
    }
}
