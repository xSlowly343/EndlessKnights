using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class AchievementState
    {
        public const int Capacity = 256;

        public HashSet<string> Unlocked { get; } = new HashSet<string>(Capacity, System.StringComparer.Ordinal);
        public List<string> Unsent { get; } = new List<string>(Capacity);
        public double[] Totals { get; } = new double[AchievementCatalog.CounterCount];
        public double[] Last { get; } = new double[AchievementCatalog.CounterCount];
    }
}
