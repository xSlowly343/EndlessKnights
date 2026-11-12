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
            MoveProgress(state.GemProgress, gems);
        }

        public BuildPreset Preset(BuildPreset preset, ClassTreeAllocation tree)
        {
            var reforged = new BuildPreset(tree) { CommonTrees = preset.CommonTrees };
            for (var i = 0; i < preset.Skills.Count; i++)
            {
                var skill = preset.Skills[i];
                if (skill.Ascendancy != null)
                    continue;
                var role = preset.RoleAt(i);
                reforged.Roles.Add(role != null && SkillLoadout.Accepts(role.Value, Of(skill)) ? role.Value : SkillLoadout.IsAura(Of(skill)) ? SkillSlotRole.Aura : SkillSlotRole.Auto);
                reforged.Skills.Add(Of(skill));
                reforged.Supports.Add(preset.SupportsAt(i));
            }
            return reforged;
        }

        private void MoveProgress(Dictionary<string, GemProgress> progress, GemCatalog gems)
        {
            var moved = new List<KeyValuePair<string, GemProgress>>();
            foreach (var id in new List<string>(progress.Keys))
            {
                if (!gems.TryGet(id, out var gem) || ReferenceEquals(Of(gem), gem))
                    continue;
                foreach (var form in gem.Forms)
                    moved.Add(new KeyValuePair<string, GemProgress>(Of(form).ProgressId, progress[id].Copy()));
                progress.Remove(id);
            }
            foreach (var pair in moved)
            {
                if (!progress.TryGetValue(pair.Key, out var kept) || pair.Value.AheadOf(kept))
                    progress[pair.Key] = pair.Value;
            }
        }
    }
}
