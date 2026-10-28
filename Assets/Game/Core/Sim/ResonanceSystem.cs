using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ResonanceSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private ResonanceCatalog _catalog;

        public ResonanceSystem(ResonanceCatalog catalog, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public ResonanceCatalog Catalog => _catalog;

        public int Level(string id) => _state.Resonance.TryGetValue(id, out var level) ? level : 0;

        public BigNumber Cost(string id) =>
            _catalog.TryGet(id, out var branch) ? branch.Cost.At(Level(id) + 1) : BigNumber.Zero;

        public void UseCatalog(ResonanceCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public Unlocks? Unlocks { get; set; }

        public ResonanceResult Raise(string id)
        {
            if (Unlocks?.IsOpen(Feature.Resonance) == false)
                return ResonanceResult.Locked;
            if (!_catalog.TryGet(id, out var branch))
                return ResonanceResult.UnknownBranch;
            var level = Level(id);
            if (level >= branch.MaxLevel)
                return ResonanceResult.MaxLevel;
            var cost = branch.Cost.At(level + 1);
            if (_state.Echo < cost)
                return ResonanceResult.NotEnoughEcho;
            _state.Echo -= cost;
            _state.Resonance[id] = level + 1;
            Rebuild();
            return ResonanceResult.Raised;
        }

        private void Rebuild()
        {
            _modifiers.Clear();
            foreach (var branch in _catalog.All)
            {
                var level = System.Math.Min(Level(branch.Id), branch.MaxLevel);
                if (level <= 0)
                    continue;
                foreach (var modifier in branch.PerLevel)
                    _modifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * level, modifier.Tags));
                foreach (var milestone in branch.Milestones)
                {
                    if (level >= milestone.Level)
                        _modifiers.AddRange(milestone.Modifiers);
                }
            }
            _layer.Set(_modifiers);
        }
    }
}
