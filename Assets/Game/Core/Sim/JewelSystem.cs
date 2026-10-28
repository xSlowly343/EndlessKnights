using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class JewelSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;

        public JewelSystem(PassiveTree tree, GameState state, StatSheet hero)
        {
            Tree = tree;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public PassiveTree Tree { get; set; }
        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
        public IReadOnlyList<JewelItem> Bag => _state.Jewels.Bag;

        public JewelItem? In(string nodeId) => _state.Jewels.Sockets.TryGetValue(nodeId, out var jewel) ? jewel : null;

        public bool IsOpen(string nodeId) =>
            Tree.TryGet(nodeId, out var node) && node.Type == TreeNodeType.Jewel && _state.Tree.IsAllocated(Tree, nodeId);

        public bool Socket(string nodeId, JewelItem jewel)
        {
            if (!IsOpen(nodeId) || !_state.Jewels.Bag.Remove(jewel))
                return false;
            if (_state.Jewels.Sockets.TryGetValue(nodeId, out var previous))
                _state.Jewels.Bag.Add(previous);
            _state.Jewels.Sockets[nodeId] = jewel;
            Rebuild();
            return true;
        }

        public bool Unsocket(string nodeId)
        {
            if (!_state.Jewels.Sockets.TryGetValue(nodeId, out var jewel))
                return false;
            _state.Jewels.Sockets.Remove(nodeId);
            _state.Jewels.Bag.Add(jewel);
            Rebuild();
            return true;
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            var scale = System.Math.Max(0, _hero.Value(CombatStats.JewelEffect, 1));
            foreach (var pair in _state.Jewels.Sockets)
            {
                if (_state.Tree.IsAllocated(Tree, pair.Key))
                    pair.Value.AddModifiers(modifiers, scale);
            }
            _layer.Set(modifiers);
        }
    }
}
