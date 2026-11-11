using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class HeroLookChooser
    {
        private readonly CosmeticSystem _cosmetics;
        private readonly Dictionary<string, string> _triedOn = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private readonly List<CosmeticDef> _options = new List<CosmeticDef>();

        public HeroLookChooser(CosmeticSystem cosmetics)
        {
            _cosmetics = cosmetics;
        }

        public HeroLook Worn => _cosmetics.Look;

        public HeroLook Shown => new HeroLook(LookOf(CosmeticCatalog.CloakSlot), LookOf(CosmeticCatalog.EyesSlot), LookOf(CosmeticCatalog.StrikeSlot));

        public CosmeticDef? ShownIn(string slot)
        {
            if (_triedOn.TryGetValue(slot, out var id) && _cosmetics.Catalog.TryGet(id, out var tried) && !_cosmetics.Owns(id))
                return tried;
            return _cosmetics.Worn(slot) ?? _cosmetics.Catalog.ByLook(slot, 0);
        }

        public bool TryingOn(string slot) => ShownIn(slot) is { } item && !_cosmetics.Owns(item.Id);

        public string Label(string slot, bool russian)
        {
            var item = ShownIn(slot);
            if (item == null)
                return string.Empty;
            var name = russian ? item.NameRu : item.NameEn;
            return _cosmetics.Owns(item.Id) ? name : Texts.Format("settings.lookTryOn", name, item.Price);
        }

        public bool Step(string slot, int step)
        {
            _options.Clear();
            foreach (var item in _cosmetics.Catalog.All)
            {
                if (item.Slot == slot && (item.ForSale || _cosmetics.Owns(item.Id)))
                    _options.Add(item);
            }
            if (_options.Count < 2)
                return false;
            var current = ShownIn(slot);
            var index = current == null ? 0 : System.Math.Max(0, _options.IndexOf(current));
            var next = _options[((index + step) % _options.Count + _options.Count) % _options.Count];
            if (_cosmetics.Owns(next.Id))
            {
                _triedOn.Remove(slot);
                _cosmetics.Equip(next.Id);
            }
            else
            {
                _triedOn[slot] = next.Id;
            }
            return true;
        }

        private byte LookOf(string slot) => (byte)(ShownIn(slot)?.Look ?? 0);
    }
}
