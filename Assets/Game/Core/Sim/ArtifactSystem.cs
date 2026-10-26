using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ArtifactSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private ArtifactCatalog _catalog;
        private RiftRules _rift;

        public ArtifactSystem(ArtifactCatalog catalog, RiftRules rift, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _rift = rift;
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public void UseCatalog(ArtifactCatalog catalog, RiftRules rift)
        {
            _catalog = catalog;
            _rift = rift;
            Rebuild();
        }

        public bool InRift => _state.Rift.Inside;
        public IReadOnlyDictionary<string, int> Owned => OwnedLevels;
        public BigNumber Currency => InRift ? _state.Rift.Shards : _state.Echo;

        public BigNumber UnlockCost => UnlockCurve.At(OwnedLevels.Count + 1);

        public bool AllUnlocked => Locked().Count == 0;

        public BigNumber LevelCost(string id) =>
            OwnedLevels.TryGetValue(id, out var level) ? LevelCurve.At(level) : BigNumber.Zero;

        private Dictionary<string, int> OwnedLevels => InRift ? _state.Rift.Artifacts : _state.Artifacts;
        private ExponentialCurve UnlockCurve => InRift ? _rift.UnlockCost : _catalog.UnlockCost;
        private ExponentialCurve LevelCurve => InRift ? _rift.LevelCost : _catalog.LevelCost;

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
            var cost = UnlockCost;
            if (locked.Count == 0 || Currency < cost)
                return null;
            Spend(cost);
            var artifact = locked[_state.Rng.NextInt(0, locked.Count)];
            OwnedLevels[artifact.Id] = 1;
            Rebuild();
            return artifact;
        }

        public bool TryLevelUp(string id)
        {
            if (!OwnedLevels.TryGetValue(id, out var level))
                return false;
            var cost = LevelCurve.At(level);
            if (Currency < cost)
                return false;
            Spend(cost);
            OwnedLevels[id] = level + 1;
            Rebuild();
            return true;
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            var owned = OwnedLevels;
            foreach (var artifact in _catalog.All)
            {
                if (!owned.TryGetValue(artifact.Id, out var level))
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
                if (!OwnedLevels.ContainsKey(artifact.Id))
                    locked.Add(artifact);
            }
            return locked;
        }

        private void Spend(BigNumber cost)
        {
            if (InRift)
                _state.Rift.Shards -= cost;
            else
                _state.Echo -= cost;
        }
    }
}
