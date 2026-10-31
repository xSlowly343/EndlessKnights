using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class SlateState
    {
        public const ulong SeedSalt = 0x51A7E5EEDUL;

        public SlateState(ulong seed)
        {
            Rng = new Rng(seed ^ SeedSalt);
        }

        public Rng Rng { get; set; }
        public long Found { get; set; }
        public List<SlateItem> Bag { get; } = new List<SlateItem>();
        public List<SlatePlacement> Board { get; } = new List<SlatePlacement>();
    }
}
