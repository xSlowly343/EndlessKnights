using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class ClanTreeBonus
    {
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private readonly List<string> _taken = new List<string>();
        private ClanTreeCatalog _catalog;

        public ClanTreeBonus(ClanTreeCatalog catalog, StatSheet hero)
        {
            _catalog = catalog;
            _layer = new ModifierLayer(hero);
        }

        public IReadOnlyList<string> Taken => _taken;
        public IReadOnlyList<Modifier> Applied => _layer.Applied;

        public void UseCatalog(ClanTreeCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public void Use(IReadOnlyList<string> taken)
        {
            _taken.Clear();
            _taken.AddRange(taken);
            Rebuild();
        }

        public void Clear() => Use(System.Array.Empty<string>());

        private void Rebuild()
        {
            _modifiers.Clear();
            foreach (var id in _taken)
            {
                if (_catalog.TryGet(id, out var node))
                    _modifiers.AddRange(node.Modifiers);
            }
            _layer.Set(_modifiers);
        }
    }
}
