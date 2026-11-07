using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class GardenBed
    {
        public string Plant { get; set; } = string.Empty;
        public double Grown { get; set; }
        public bool Ready { get; set; }
        public bool Empty => Plant.Length == 0;
    }

    public sealed class GardenState
    {
        public List<GardenBed> Beds { get; } = new List<GardenBed>();
        public long Harvests { get; set; }
        public long Mutations { get; set; }
        public HashSet<string> Album { get; } = new HashSet<string>(System.StringComparer.Ordinal);
    }
}
