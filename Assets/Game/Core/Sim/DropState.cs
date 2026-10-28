using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class DropState
    {
        public const ulong SeedSalt = 0xD20951A7C0FFEEUL;

        public DropState(ulong seed)
        {
            Rng = new Rng(seed ^ SeedSalt);
        }

        public Rng Rng { get; set; }
        public long Day { get; set; }
        public int DailyItems { get; set; }
        public double Dust { get; set; }
        public long Shards { get; set; }
        public long Ingots { get; set; }
        public double ShardCarry { get; set; }
        public long ShardsFound { get; set; }
        public int DailyShards { get; set; }
        public int UncutGems { get; set; }
        public long UncutFound { get; set; }
        public ItemRarity? SalvageUpTo { get; set; }
        public long ItemsFound { get; set; }
        public long SalvagedItems { get; set; }
        public List<GearItem> Gear { get; } = new List<GearItem>();
    }
}
