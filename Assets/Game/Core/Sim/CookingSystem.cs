using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum CookingResult
    {
        Done,
        Closed,
        UnknownDish,
        Busy,
        NoIngredients,
        NoMeal,
        MealsFull
    }

    public sealed class CookingSystem
    {
        public const string Activity = "cooking";

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(4);
        private CookingRules _rules;

        public CookingSystem(CookingRules rules, GameState state, StatSheet hero, CampSystem camp, PeacefulSystem peaceful, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _peaceful = peaceful;
            _events = events;
            _layer = new ModifierLayer(hero, ModifierPool.World);
            ApplyActive();
        }

        public CookingRules Rules => _rules;
        public CookingState State => _state.Cooking;
        public bool Open => _camp.Open(_rules.Building);
        public bool Busy => State.Cooking.Length > 0;

        public void UseRules(CookingRules rules)
        {
            _rules = rules;
            ApplyActive();
        }

        public bool HasIngredients(DishDef dish)
        {
            for (var i = 0; i < dish.Ingredients.Count; i++)
            {
                if (_camp.Count(dish.Ingredients[i]) < Needed(dish, dish.Ingredients[i]))
                    return false;
            }
            return true;
        }

        public bool Favorite(string dish) => _state.ClassChosen && _rules.FavoriteOf(_state.Class) == dish;

        public double SecondsOf(DishDef dish) => dish.Seconds * (Favorite(dish.Id) ? _rules.FavoriteMultiplier : 1) * (1 + _state.Perk(MuseumPerk.MealLonger));

        public double ChopsOf(DishDef dish) => System.Math.Ceiling(dish.Chops * System.Math.Max(0.1, 1 - _state.Perk(MuseumPerk.FewerChops)) * _state.PeacefulBonus.TimeFactor);

        public CookingResult Cook(string dishId)
        {
            if (!Open)
                return CookingResult.Closed;
            var dish = _rules.Dish(dishId);
            if (dish == null)
                return CookingResult.UnknownDish;
            if (Busy)
                return CookingResult.Busy;
            if (State.Meals.Count >= _rules.MealsKept)
                return CookingResult.MealsFull;
            if (!HasIngredients(dish))
                return CookingResult.NoIngredients;
            foreach (var ingredient in dish.Ingredients)
            {
                if (_state.Rng.NextDouble() >= _state.Perk(MuseumPerk.IngredientBack))
                    _camp.Take(ingredient, 1);
            }
            State.Cooking = dish.Id;
            State.Chops = 0;
            State.Plates = 0;
            return CookingResult.Done;
        }

        public CookingResult Eat(int index)
        {
            if (index < 0 || index >= State.Meals.Count)
                return CookingResult.NoMeal;
            var dish = _rules.Dish(State.Meals[index]);
            State.Meals.RemoveAt(index);
            if (dish == null)
                return CookingResult.UnknownDish;
            State.Active = dish.Id;
            State.ActiveTicksLeft = (long)System.Math.Round(SecondsOf(dish) * GameLoop.TicksPerSecond);
            ApplyActive();
            return CookingResult.Done;
        }

        public bool AddMeal(string dishId)
        {
            if (_rules.Dish(dishId) == null || State.Meals.Count >= _rules.MealsKept)
                return false;
            State.Meals.Add(dishId);
            return true;
        }

        public void Tick()
        {
            if (!Busy || !_peaceful.InCamp || _peaceful.Activity != Activity)
                return;
            var work = _peaceful.Work(Activity);
            var dish = _rules.Dish(State.Cooking);
            if (dish == null)
            {
                State.Cooking = string.Empty;
                return;
            }
            var chops = ChopsOf(dish);
            State.Chops = System.Math.Min(chops, State.Chops + work.Keys);
            if (State.Chops >= chops)
                State.Plates += work.Clicks;
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            if (State.Chops < chops || State.Plates < dish.Plates)
                return;
            State.Meals.Add(dish.Id);
            if (_peaceful.InHideout && _state.Hideout.PotBuilt)
                State.SharedDishes.Add(dish.Id);
            if (State.Meals.Count < _rules.MealsKept && _state.Rng.NextDouble() < _state.Perk(MuseumPerk.DoubleMeal))
                State.Meals.Add(dish.Id);
            State.Book.Add(dish.Id);
            State.Cooked++;
            State.Cooking = string.Empty;
            State.Chops = 0;
            State.Plates = 0;
            _events.Add(GameEvent.ForStage(GameEventKind.DishCooked, _state.Tick, IndexOf(dish.Id)));
        }

        public void BattleTick()
        {
            if (State.Active.Length == 0)
                return;
            if (--State.ActiveTicksLeft > 0)
                return;
            State.Active = string.Empty;
            State.ActiveTicksLeft = 0;
            ApplyActive();
            _events.Add(GameEvent.ForStage(GameEventKind.MealEnded, _state.Tick, 0));
        }

        private static int Needed(DishDef dish, string ingredient)
        {
            var count = 0;
            for (var i = 0; i < dish.Ingredients.Count; i++)
            {
                if (dish.Ingredients[i] == ingredient)
                    count++;
            }
            return count;
        }

        private int IndexOf(string dish)
        {
            for (var i = 0; i < _rules.Dishes.Count; i++)
            {
                if (_rules.Dishes[i].Id == dish)
                    return i;
            }
            return -1;
        }

        private void ApplyActive()
        {
            _buffer.Clear();
            var dish = State.Active.Length == 0 ? null : _rules.Dish(State.Active);
            if (dish != null)
                _buffer.AddRange(dish.Modifiers);
            _layer.Set(_buffer);
        }
    }
}
