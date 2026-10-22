using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ArtifactSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private ArtifactCatalog _catalog;

        public ArtifactSystem(ArtifactCatalog catalog, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public void UseCatalog(ArtifactCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public BigNumber UnlockCost => _catalog.UnlockCost.At(_state.Artifacts.Count + 1);

        public bool AllUnlocked => Locked().Count == 0;

        public BigNumber LevelCost(string id) =>
            _state.Artifacts.TryGetValue(id, out var level) ? _catalog.LevelCost.At(level) : BigNumber.Zero;

        public IReadOnlyList<(ArtifactDef Artifact, double Chance)> Chances()
        {
            var locked = Locked();
            var chances = new List<(ArtifactDef, double)>(locked.Count);
            foreach (var artifact in locked)
                chances.Add((artifact, 1.0 / locked.Count));
            return chances;
        }

        public ArtifactDef? TryUnlock()
        {
            var locked = Locked();
            if (locked.Count == 0 || _state.Echo < UnlockCost)
                return null;
            _state.Echo -= UnlockCost;
            var artifact = locked[_state.Rng.NextInt(0, locked.Count)];
            _state.Artifacts[artifact.Id] = 1;
            Rebuild();
            return artifact;
        }

        public bool TryLevelUp(string id)
        {
            if (!_state.Artifacts.TryGetValue(id, out var level))
                return false;
            var cost = _catalog.LevelCost.At(level);
            if (_state.Echo < cost)
                return false;
            _state.Echo -= cost;
            _state.Artifacts[id] = level + 1;
            Rebuild();
            return true;
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            foreach (var artifact in _catalog.All)
            {
                if (!_state.Artifacts.TryGetValue(artifact.Id, out var level))
                    continue;
                foreach (var modifier in artifact.PerLevel)
                    modifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * level, modifier.Tags));
                modifiers.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, _catalog.AllDamagePerLevel * level));
            }
            _layer.Set(modifiers);
        }

        private List<ArtifactDef> Locked()
        {
            var locked = new List<ArtifactDef>();
            foreach (var artifact in _catalog.All)
            {
                if (!_state.Artifacts.ContainsKey(artifact.Id))
                    locked.Add(artifact);
            }
            return locked;
        }
    }
}
