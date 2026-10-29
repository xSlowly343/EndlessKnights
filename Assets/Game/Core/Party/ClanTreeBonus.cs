using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class ClanTreeBonus
    {
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private readonly ClanBonusState _saved;
        private ClanTreeCatalog _catalog;

        public ClanTreeBonus(ClanTreeCatalog catalog, StatSheet hero, ClanBonusState saved)
        {
            _catalog = catalog;
            _saved = saved;
            _layer = new ModifierLayer(hero);
        }

        public IReadOnlyList<string> Taken => _saved.Nodes;
        public IReadOnlyList<Modifier> Applied => _layer.Applied;
        public bool Active { get; private set; }
        public long AnsweredUnixMs => _saved.AnsweredUnixMs;

        public void UseCatalog(ClanTreeCatalog catalog)
        {
            _catalog = catalog;
            if (Active)
                Apply();
        }

        public void Use(IReadOnlyList<string> taken, long nowUnixMs)
        {
            _saved.Nodes.Clear();
            for (var i = 0; i < taken.Count; i++)
                _saved.Nodes.Add(taken[i]);
            _saved.AnsweredUnixMs = nowUnixMs;
            _saved.SeenUnixMs = nowUnixMs;
            Active = false;
            Refresh(nowUnixMs);
        }

        public void Clear(long nowUnixMs) => Use(System.Array.Empty<string>(), nowUnixMs);

        public bool Refresh(long nowUnixMs)
        {
            if (nowUnixMs > _saved.SeenUnixMs)
                _saved.SeenUnixMs = nowUnixMs;
            var fresh = _saved.Nodes.Count > 0 && _saved.SeenUnixMs - _saved.AnsweredUnixMs <= _catalog.OfflineHours * 3_600_000L;
            if (fresh && !Active)
                Apply();
            else if (!fresh && _layer.Applied.Count > 0)
                _layer.Set(System.Array.Empty<Modifier>());
            Active = fresh;
            return fresh;
        }

        private void Apply()
        {
            _modifiers.Clear();
            foreach (var id in _saved.Nodes)
            {
                if (_catalog.TryGet(id, out var node))
                    _modifiers.AddRange(node.Modifiers);
            }
            _layer.Set(_modifiers);
        }
    }
}
