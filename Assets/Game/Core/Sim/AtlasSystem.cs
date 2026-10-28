using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class AtlasSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private AtlasCatalog _catalog;

        public AtlasSystem(AtlasCatalog catalog, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public AtlasCatalog Catalog => _catalog;

        public bool Taken(string id) => _state.Atlas.Contains(id);

        public void UseCatalog(AtlasCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public Unlocks? Unlocks { get; set; }

        public AtlasResult CanAllocate(string id)
        {
            if (Unlocks?.IsOpen(Feature.Atlas) == false)
                return AtlasResult.Locked;
            if (!_catalog.TryGet(id, out var node))
                return AtlasResult.UnknownNode;
            if (Taken(id))
                return AtlasResult.AlreadyTaken;
            if (!Connected(node))
                return AtlasResult.NotConnected;
            if (_state.Echo < BigNumber.FromDouble(node.Cost))
                return AtlasResult.NotEnoughEcho;
            return AtlasResult.Allocated;
        }

        public AtlasResult Allocate(string id)
        {
            var result = CanAllocate(id);
            if (result != AtlasResult.Allocated)
                return result;
            _catalog.TryGet(id, out var node);
            _state.Echo -= BigNumber.FromDouble(node.Cost);
            _state.Atlas.Add(id);
            Rebuild();
            return AtlasResult.Allocated;
        }

        private bool Connected(AtlasNode node)
        {
            if (node.Requires.Count == 0)
                return true;
            foreach (var parent in node.Requires)
            {
                if (Taken(parent))
                    return true;
            }
            return false;
        }

        private void Rebuild()
        {
            _modifiers.Clear();
            foreach (var id in _state.Atlas)
            {
                if (_catalog.TryGet(id, out var node))
                    _modifiers.AddRange(node.Modifiers);
            }
            _layer.Set(_modifiers);
        }
    }
}
