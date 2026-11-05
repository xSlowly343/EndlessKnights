using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum CosmeticResult
    {
        Done,
        Unknown,
        NotOwned,
        ClanOnly
    }

    public sealed class CosmeticSystem
    {
        private readonly GameState _state;
        private CosmeticCatalog _catalog;

        public CosmeticSystem(CosmeticCatalog catalog, GameState state)
        {
            _catalog = catalog;
            _state = state;
        }

        public CosmeticCatalog Catalog => _catalog;
        public CosmeticState State => _state.Cosmetics;

        public void UseCatalog(CosmeticCatalog catalog) => _catalog = catalog;

        public bool Owns(string id) => State.Owned.Contains(id);

        public bool Grant(string id)
        {
            if (!_catalog.TryGet(id, out _))
                return false;
            return State.Owned.Add(id);
        }

        public void SetOwned(IEnumerable<string> ids)
        {
            State.Owned.Clear();
            foreach (var id in ids)
            {
                if (_catalog.TryGet(id, out _))
                    State.Owned.Add(id);
            }
            var stale = new List<string>();
            foreach (var pair in State.Equipped)
            {
                if (!State.Owned.Contains(pair.Value))
                    stale.Add(pair.Key);
            }
            foreach (var key in stale)
                State.Equipped.Remove(key);
        }

        public CosmeticResult Equip(string id)
        {
            if (!_catalog.TryGet(id, out var item))
                return CosmeticResult.Unknown;
            if (item.Clan)
                return CosmeticResult.ClanOnly;
            if (!Owns(id))
                return CosmeticResult.NotOwned;
            State.Equipped[item.EquipKey] = id;
            return CosmeticResult.Done;
        }

        public bool Unequip(string equipKey) => State.Equipped.Remove(equipKey);

        public bool IsEquipped(string id) => _catalog.TryGet(id, out var item) && State.Equipped.TryGetValue(item.EquipKey, out var worn) && worn == id;

        public CosmeticDef? Worn(string slot, string heroOrWeaponType = "")
        {
            var key = heroOrWeaponType.Length > 0 ? slot + ":" + heroOrWeaponType : slot;
            return State.Equipped.TryGetValue(key, out var id) && _catalog.TryGet(id, out var item) ? item : null;
        }
    }
}
