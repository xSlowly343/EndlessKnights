using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ArtifactSystem
    {
        private const double MinUnlockDiscount = 0.1;
        private static readonly int TypeCount = System.Enum.GetValues(typeof(ArtifactType)).Length;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private ArtifactCatalog _catalog;
        private RiftRules _rift;

        public ArtifactSystem(ArtifactCatalog catalog, RiftRules rift, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _rift = rift;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public void UseCatalog(ArtifactCatalog catalog, RiftRules rift)
        {
            _catalog = catalog;
            _rift = rift;
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
        public System.Action? Rebuilt { get; set; }

        public bool InRift => _state.Rift.Inside;
        public IReadOnlyDictionary<string, int> Owned => OwnedLevels;
        public BigNumber Currency => InRift ? _state.Rift.Shards : _state.Echo;

        public BigNumber UnlockCost => UnlockCurve.At(OwnedLevels.Count + 1) * UnlockDiscount;

        public bool AllUnlocked => OwnedLevels.Count >= _catalog.All.Count && Locked().Count == 0;

        public bool WaitsForGroup => !AllUnlocked && Locked().Count == 0;

        public int OpenGroup
        {
            get
            {
                var group = int.MaxValue;
                foreach (var artifact in _catalog.All)
                {
                    if (!OwnedLevels.ContainsKey(artifact.Id))
                        group = System.Math.Min(group, artifact.Group);
                }
                return group == int.MaxValue ? _catalog.Groups : group;
            }
        }

        public BigNumber LevelCost(string id) =>
            OwnedLevels.TryGetValue(id, out var level) ? LevelCurve.At(level) : BigNumber.Zero;

        public bool IsMaxed(string id) =>
            OwnedLevels.TryGetValue(id, out var level) && _catalog.TryGet(id, out var artifact) && artifact.Capped && level >= artifact.MaxLevel;

        private double UnlockDiscount => System.Math.Max(MinUnlockDiscount, _hero.Value(CombatStats.ArtifactCost, 1));

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

        public Unlocks? Unlocks { get; set; }

        public bool Open => InRift || Unlocks?.IsOpen(Feature.Artifacts) != false;

        public ArtifactDef? TryUnlock()
        {
            if (!Open)
                return null;
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
            if (!Open || !OwnedLevels.TryGetValue(id, out var level) || IsMaxed(id))
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
            var boosts = TypeBoosts(owned, out var allDamageBoost);
            foreach (var artifact in _catalog.All)
            {
                if (!owned.TryGetValue(artifact.Id, out var saved))
                    continue;
                var level = artifact.EffectiveLevel(saved);
                var scale = artifact.Type == ArtifactType.Meta ? 1 : 1 + boosts[(int)artifact.Type];
                if (artifact.WorksFor(_state.Class))
                {
                    foreach (var modifier in artifact.PerLevel)
                        modifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * level * scale, modifier.Tags));
                    foreach (var modifier in artifact.PerArtifact)
                        modifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * level * owned.Count * scale, modifier.Tags));
                }
                modifiers.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, _catalog.AllDamagePerLevel * level * (1 + allDamageBoost)));
            }
            _layer.Set(modifiers);
            Rebuilt?.Invoke();
        }

        private double[] TypeBoosts(Dictionary<string, int> owned, out double allDamageBoost)
        {
            var boosts = new double[TypeCount];
            allDamageBoost = 0;
            foreach (var artifact in _catalog.All)
            {
                if (!owned.TryGetValue(artifact.Id, out var saved))
                    continue;
                var level = artifact.EffectiveLevel(saved);
                allDamageBoost += artifact.BoostAllDamage * level;
                foreach (var type in artifact.BoostTypes)
                    boosts[(int)type] += artifact.Boost * level;
            }
            return boosts;
        }

        private List<ArtifactDef> Locked()
        {
            var locked = new List<ArtifactDef>();
            var group = int.MaxValue;
            foreach (var artifact in _catalog.All)
            {
                if (!OwnedLevels.ContainsKey(artifact.Id))
                    group = System.Math.Min(group, artifact.Group);
            }
            if (Unlocks != null && group != int.MaxValue && !Unlocks.ArtifactGroupOpen(group))
                return locked;
            foreach (var artifact in _catalog.All)
            {
                if (artifact.Group == group && !OwnedLevels.ContainsKey(artifact.Id))
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
