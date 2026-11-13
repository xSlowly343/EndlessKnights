using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum RiftSupplyResult
    {
        Done,
        Unknown,
        NotOwned
    }

    public sealed class RiftSupplySystem
    {
        public const string DishPrefix = "dish:";
        public const string PotionPrefix = "potion:";
        public const string FineSuffix = ":fine";

        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(8);
        private CookingRules _cooking;
        private AlchemyRules _alchemy;
        private bool _applied;

        public RiftSupplySystem(CookingRules cooking, AlchemyRules alchemy, GameState state, StatSheet hero)
        {
            _cooking = cooking;
            _alchemy = alchemy;
            _state = state;
            _layer = new ModifierLayer(hero, ModifierPool.World);
        }

        public string Supply => _state.Rift.Supply;
        public bool HasSupply => _state.Rift.Supply.Length > 0 && _state.Rift.SupplyWindow == _state.Rift.WindowId;
        public bool Active => HasSupply && _state.Rift.Inside;

        public void UseRules(CookingRules cooking, AlchemyRules alchemy)
        {
            _cooking = cooking;
            _alchemy = alchemy;
            _applied = !Active;
            Tick();
        }

        public bool Known(string item) => Modifiers(item, null);

        public RiftSupplyResult Pack(string item)
        {
            if (!Known(item))
                return RiftSupplyResult.Unknown;
            if (item.StartsWith(DishPrefix, System.StringComparison.Ordinal))
                return _state.Cooking.Meals.Remove(item.Substring(DishPrefix.Length)) ? RiftSupplyResult.Done : RiftSupplyResult.NotOwned;
            var (recipe, fine) = PotionOf(item);
            var potions = _state.Alchemy.Potions;
            for (var i = potions.Count - 1; i >= 0; i--)
            {
                if (potions[i].Recipe != recipe || potions[i].Fine != fine || potions[i].Unknown)
                    continue;
                potions.RemoveAt(i);
                return RiftSupplyResult.Done;
            }
            return RiftSupplyResult.NotOwned;
        }

        public void Unpack(string item)
        {
            if (!Known(item))
                return;
            if (item.StartsWith(DishPrefix, System.StringComparison.Ordinal))
            {
                _state.Cooking.Meals.Add(item.Substring(DishPrefix.Length));
                return;
            }
            var (recipe, fine) = PotionOf(item);
            _state.Alchemy.Potions.Add(new Potion { Recipe = recipe, Fine = fine });
        }

        public void Receive(long windowId, string item)
        {
            if (!Known(item))
                return;
            _state.Rift.Supply = item;
            _state.Rift.SupplyWindow = windowId;
            _applied = !Active;
            Tick();
        }

        public void Tick()
        {
            var active = Active;
            if (active == _applied)
                return;
            _applied = active;
            _buffer.Clear();
            if (active)
                Modifiers(_state.Rift.Supply, _buffer);
            _layer.Set(_buffer);
        }

        public IReadOnlyList<Modifier> ModifiersOf(string item)
        {
            var list = new List<Modifier>();
            Modifiers(item, list);
            return list;
        }

        private bool Modifiers(string item, List<Modifier>? into)
        {
            if (item.StartsWith(DishPrefix, System.StringComparison.Ordinal))
            {
                var dish = _cooking.Dish(item.Substring(DishPrefix.Length));
                if (dish == null)
                    return false;
                into?.AddRange(dish.Modifiers);
                return true;
            }
            if (!item.StartsWith(PotionPrefix, System.StringComparison.Ordinal))
                return false;
            var (id, fine) = PotionOf(item);
            var recipe = _alchemy.Recipe(id);
            if (recipe == null)
                return false;
            if (into != null)
            {
                var times = fine ? _alchemy.FineMultiplier : 1;
                foreach (var modifier in recipe.Modifiers)
                    into.Add(modifier.WithValue(modifier.Value * times));
            }
            return true;
        }

        private static (string Recipe, bool Fine) PotionOf(string item)
        {
            var fine = item.EndsWith(FineSuffix, System.StringComparison.Ordinal);
            return (item.Substring(PotionPrefix.Length, item.Length - PotionPrefix.Length - (fine ? FineSuffix.Length : 0)), fine);
        }
    }
}
