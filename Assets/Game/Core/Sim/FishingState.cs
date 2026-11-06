using System.Collections.Generic;

namespace Game.Core.Sim
{
    public enum FishingPhase
    {
        Idle,
        Waiting,
        Bite,
        Fight
    }

    public enum FishCatchKind
    {
        Fish,
        Legend,
        Bottle
    }

    public sealed class FishRecord
    {
        public long Count { get; set; }
        public double BestCm { get; set; }
    }

    public sealed class FishingState
    {
        public string Spot { get; set; } = string.Empty;
        public FishingPhase Phase { get; set; }
        public double Timer { get; set; }
        public FishCatchKind HookedKind { get; set; }
        public string Hooked { get; set; } = string.Empty;
        public double Reeled { get; set; }
        public Dictionary<string, FishRecord> Album { get; } = new Dictionary<string, FishRecord>(System.StringComparer.Ordinal);
        public Dictionary<string, double> LegendTired { get; } = new Dictionary<string, double>(System.StringComparer.Ordinal);
        public long Caught { get; set; }
        public long Missed { get; set; }
        public long Escaped { get; set; }
        public long Bottles { get; set; }
        public string LastCatch { get; set; } = string.Empty;
        public double LastCm { get; set; }
    }
}
