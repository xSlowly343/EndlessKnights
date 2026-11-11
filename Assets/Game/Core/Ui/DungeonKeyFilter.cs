using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public enum DungeonKeyKind
    {
        Any,
        Home,
        Rift
    }

    public sealed class DungeonKeyFilter
    {
        public DungeonKeyKind Kind { get; set; } = DungeonKeyKind.Any;
        public int MinTier { get; set; } = 1;
        public int MaxTier { get; set; } = int.MaxValue;
        public bool FreshOnly { get; set; }
        public string? WithModifier { get; set; }
        public string? WithoutModifier { get; set; }

        public bool Matches(DungeonSystem dungeons, DungeonKey key)
        {
            if (Kind != DungeonKeyKind.Any && key.ForRift != (Kind == DungeonKeyKind.Rift))
                return false;
            if (key.Tier < MinTier || key.Tier > MaxTier)
                return false;
            if (FreshOnly && key.Attempts < dungeons.Rules.KeyAttempts)
                return false;
            if (WithModifier != null && !Has(key, WithModifier))
                return false;
            return WithoutModifier == null || !Has(key, WithoutModifier);
        }

        public void Collect(DungeonSystem dungeons, List<DungeonKey> shown)
        {
            shown.Clear();
            foreach (var key in dungeons.Keys)
            {
                if (Matches(dungeons, key))
                    shown.Add(key);
            }
            shown.Sort((a, b) => a.ForRift != b.ForRift ? a.ForRift.CompareTo(b.ForRift)
                : a.Tier != b.Tier ? b.Tier.CompareTo(a.Tier)
                : dungeons.StageOf(b).CompareTo(dungeons.StageOf(a)));
        }

        private static bool Has(DungeonKey key, string modifier)
        {
            foreach (var id in key.Modifiers)
            {
                if (id == modifier)
                    return true;
            }
            return false;
        }
    }
}
