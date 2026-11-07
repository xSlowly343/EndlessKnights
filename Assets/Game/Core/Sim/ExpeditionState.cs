using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class Expedition
    {
        public string Companion { get; set; } = string.Empty;
        public string Partner { get; set; } = string.Empty;
        public string Third { get; set; } = string.Empty;
        public bool Pack => Third.Length > 0;
        public bool Colossus { get; set; }
        public string Treat { get; set; } = string.Empty;
        public string Biome { get; set; } = string.Empty;
        public int Hours { get; set; }
        public double Seconds { get; set; }
        public double Planned { get; set; }
        public double Elapsed { get; set; }
        public int Letter { get; set; } = -1;
        public bool Choice { get; set; }
        public bool Waited { get; set; }
        public long[] Items { get; set; } = System.Array.Empty<long>();
        public long Shards { get; set; }
        public string Rare { get; set; } = string.Empty;
        public bool Done { get; set; }
        public bool LetterSent => Letter >= 0;
    }

    public sealed class ExpeditionState
    {
        public const int ReportsKept = 10;

        public List<Expedition> Active { get; } = new List<Expedition>();
        public List<Expedition> Reports { get; } = new List<Expedition>();
        public long Returned { get; set; }
        public HashSet<string> Finds { get; } = new HashSet<string>(System.StringComparer.Ordinal);
    }
}
