using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public sealed class GemReforge
    {
        private readonly ReforgeTable _table;
        private readonly WeaponType _from;
        private readonly WeaponType _to;

        public GemReforge(ReforgeTable table, WeaponType from, WeaponType to)
        {
            _table = table;
            _from = from;
            _to = to;
        }

        public GemDef Of(GemDef gem) => _table.Gem(gem, _from, _to) ?? gem;

        public void Apply(GameState state, GemCatalog gems)
        {
            var owned = state.OwnedGems;
            for (var i = 0; i < owned.Count; i++)
                owned[i] = Of(owned[i]);
            MoveBound(state.BoundGems, gems);
            MoveProgress(state.GemProgress, gems);
        }

        public BuildPreset Preset(BuildPreset preset, TreeAllocation tree)
        {
            var reforged = new BuildPreset(tree) { CommonTrees = preset.CommonTrees };
            for (var i = 0; i < preset.Skills.Count; i++)
            {
                var (skill, trigger) = preset.Skills[i];
                if (skill.Ascendancy != null)
                    continue;
                reforged.Skills.Add((Of(skill), trigger));
                reforged.Supports.Add(preset.SupportsAt(i));
            }
            return reforged;
        }

        private void MoveBound(Dictionary<string, int> bound, GemCatalog gems)
        {
            foreach (var id in new List<string>(bound.Keys))
            {
                if (!gems.TryGet(id, out var gem) || ReferenceEquals(Of(gem), gem))
                    continue;
                var count = bound[id];
                bound.Remove(id);
                bound[Of(gem).Id] = (bound.TryGetValue(Of(gem).Id, out var already) ? already : 0) + count;
            }
        }

        private void MoveProgress(Dictionary<string, GemProgress> progress, GemCatalog gems)
        {
            var moved = new List<KeyValuePair<string, GemProgress>>();
            foreach (var id in new List<string>(progress.Keys))
            {
                if (!gems.TryGet(id, out var gem) || ReferenceEquals(Of(gem), gem))
                    continue;
                moved.Add(new KeyValuePair<string, GemProgress>(Of(gem).Id, progress[id]));
                progress.Remove(id);
            }
            foreach (var pair in moved)
            {
                if (!progress.TryGetValue(pair.Key, out var kept) || Ahead(pair.Value, kept))
                    progress[pair.Key] = pair.Value;
            }
        }

        private static bool Ahead(GemProgress a, GemProgress b) =>
            a.Rebirths != b.Rebirths ? a.Rebirths > b.Rebirths : a.Level != b.Level ? a.Level > b.Level : a.Experience > b.Experience;
    }
}
