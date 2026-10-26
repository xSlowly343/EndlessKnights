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
        public ItemRarity? SalvageUpTo { get; set; }
        public long ItemsFound { get; set; }
        public long SalvagedItems { get; set; }
        public List<GearItem> Gear { get; } = new List<GearItem>();
    }
}
