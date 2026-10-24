using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class NativeTrait
    {
        public static readonly NativeTrait None = new NativeTrait();

        public IReadOnlyList<Modifier> Modifiers { get; set; } = System.Array.Empty<Modifier>();
        public double HoldDecaySeconds { get; set; }
        public double SpendAllPerPoint { get; set; }
        public double SpendAllMax { get; set; }
        public double ResourcePerKeyHit { get; set; }
        public double WoundedSpawnResource { get; set; }
        public double HoldStacksAboveResource { get; set; }
        public double FullResourceDurationMultiplier { get; set; }
        public string? ExtraTargetWhileFamily { get; set; }
        public string? RefreshFamily { get; set; }

        public bool SpendsAll => SpendAllPerPoint > 0;
        public bool HoldsStacks => HoldStacksAboveResource > 0;
    }
}
