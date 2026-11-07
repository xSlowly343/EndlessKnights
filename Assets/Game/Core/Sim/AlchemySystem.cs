using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum AlchemyResult
    {
        Done,
        UnknownRecipe,
        NoHerbs,
        Busy,
        NoPotion
    }

    public sealed class AlchemySystem
    {
        public const string Activity = "alchemy";

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(4);
        private AlchemyRules _rules;

        public AlchemySystem(AlchemyRules rules, GameState state, StatSheet hero, CampSystem camp, PeacefulSystem peaceful, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _peaceful = peaceful;
            _events = events;
            _layer = new ModifierLayer(hero);
            ApplyActive();
        }

        public AlchemyRules Rules => _rules;
        public AlchemyState State => _state.Alchemy;
        public bool Brewing => State.Brewing != null;
        public HideoutCrafts? Shared { get; set; }
        public bool Together => _peaceful.InHideout && _state.Hideout.Cauldron.Together && Shared != null;
        public double StirsNeeded(PotionRecipe recipe) => recipe.Stirs * (Together ? 1 - Shared!.StirsShare : 1);

        public void UseRules(AlchemyRules rules)
        {
            _rules = rules;
            ApplyActive();
        }

        public bool Knows(string recipe) => State.Known.Contains(recipe) || _rules.Recipe(recipe)?.Known == true;

        public AlchemyResult Brew(string recipeId)
        {
            var recipe = _rules.Recipe(recipeId);
            if (recipe == null || !Knows(recipeId))
                return AlchemyResult.UnknownRecipe;
            return Start(recipe.Herbs, recipe, false);
        }

        public AlchemyResult Experiment(IReadOnlyList<string> herbs)
        {
            if (herbs.Count == 0)
                return AlchemyResult.NoHerbs;
            var recipe = _rules.RecipeOf(herbs);
            if (recipe != null)
            {
                var result = Start(herbs, recipe, false);
                if (result == AlchemyResult.Done)
                    State.Known.Add(recipe.Id);
                return result;
            }
            return Start(herbs, Unlearned(), true);
        }

        public AlchemyResult Drink(int index)
        {
            if (index < 0 || index >= State.Potions.Count)
                return AlchemyResult.NoPotion;
            var potion = State.Potions[index];
            State.Potions.RemoveAt(index);
            var recipe = _rules.Recipe(potion.Recipe);
            if (recipe == null)
                return AlchemyResult.UnknownRecipe;
            if (potion.Unknown && State.Known.Add(recipe.Id))
                _events.Add(GameEvent.ForStage(GameEventKind.RecipeLearned, _state.Tick, IndexOf(recipe.Id)));
            State.Active = new Potion { Recipe = recipe.Id, Fine = potion.Fine };
            State.ActiveTicksLeft = (long)System.Math.Round(recipe.Seconds * System.Math.Max(1, potion.Lasting) * GameLoop.TicksPerSecond);
            ApplyActive();
            return AlchemyResult.Done;
        }

        public void Tick()
        {
            if (State.Brewing == null || !_peaceful.InCamp || _peaceful.Activity != Activity || (_peaceful.InHideout && !_state.Hideout.Cauldron.Built))
                return;
            var work = _peaceful.Work(Activity);
            State.Stirs += work.Keys * (1 + _state.Perk(MuseumPerk.StirFaster));
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            var recipe = _rules.Recipe(State.Brewing.Recipe);
            if (recipe == null || State.Stirs < StirsNeeded(recipe))
                return;
            State.Potions.Add(State.Brewing);
            if (Together && !State.Brewing.Unknown)
                State.SharedBrews.Add(new Potion { Recipe = State.Brewing.Recipe, Fine = State.Brewing.Fine });
            if (_state.Rng.NextDouble() < _state.Perk(MuseumPerk.DoublePotion))
                State.Potions.Add(new Potion { Recipe = State.Brewing.Recipe, Fine = State.Brewing.Fine, Unknown = State.Brewing.Unknown });
            State.Brewing = null;
            State.Stirs = 0;
            _events.Add(GameEvent.ForStage(GameEventKind.PotionBrewed, _state.Tick, IndexOf(recipe.Id)));
        }

        public void BattleTick()
        {
            if (State.Active == null)
                return;
            if (--State.ActiveTicksLeft > 0)
                return;
            State.Active = null;
            State.ActiveTicksLeft = 0;
            ApplyActive();
            _events.Add(GameEvent.ForStage(GameEventKind.PotionEnded, _state.Tick, 0));
        }

        private AlchemyResult Start(IReadOnlyList<string> herbs, PotionRecipe recipe, bool unknown)
        {
            if (Brewing)
                return AlchemyResult.Busy;
            if (!HasHerbs(herbs))
                return AlchemyResult.NoHerbs;
            foreach (var herb in herbs)
                _camp.Take(herb, 1);
            var fineChance = _state.Perk(MuseumPerk.FinePotion) + (Together ? Shared!.FineChance : 0);
            State.Brewing = new Potion { Recipe = recipe.Id, Fine = _rules.Fine(herbs) || _state.Rng.NextDouble() < fineChance, Unknown = unknown };
            State.Stirs = 0;
            return AlchemyResult.Done;
        }

        private bool HasHerbs(IReadOnlyList<string> herbs)
        {
            var need = new Dictionary<string, long>(System.StringComparer.Ordinal);
            foreach (var herb in herbs)
                need[herb] = (need.TryGetValue(herb, out var count) ? count : 0) + 1;
            foreach (var pair in need)
            {
                if (_camp.Count(pair.Key) < pair.Value)
                    return false;
            }
            return true;
        }

        public bool Teach()
        {
            for (var i = 0; i < _rules.Recipes.Count; i++)
            {
                if (Knows(_rules.Recipes[i].Id))
                    continue;
                var recipe = Unlearned();
                State.Known.Add(recipe.Id);
                _events.Add(GameEvent.ForStage(GameEventKind.RecipeLearned, _state.Tick, IndexOf(recipe.Id)));
                return true;
            }
            return false;
        }

        public int PotionsOf(string recipe)
        {
            var count = 0;
            for (var i = 0; i < State.Potions.Count; i++)
            {
                if (State.Potions[i].Recipe == recipe && !State.Potions[i].Unknown)
                    count++;
            }
            return count;
        }

        public bool TakePotions(string recipe, int amount)
        {
            if (amount <= 0 || PotionsOf(recipe) < amount)
                return false;
            for (var i = State.Potions.Count - 1; i >= 0 && amount > 0; i--)
            {
                if (State.Potions[i].Recipe != recipe || State.Potions[i].Unknown)
                    continue;
                State.Potions.RemoveAt(i);
                amount--;
            }
            return true;
        }

        private PotionRecipe Unlearned()
        {
            var unknown = new List<PotionRecipe>();
            foreach (var recipe in _rules.Recipes)
            {
                if (!Knows(recipe.Id))
                    unknown.Add(recipe);
            }
            var pool = unknown.Count > 0 ? unknown : new List<PotionRecipe>(_rules.Recipes);
            return pool[_state.Rng.NextInt(0, pool.Count)];
        }

        private int IndexOf(string recipe)
        {
            for (var i = 0; i < _rules.Recipes.Count; i++)
            {
                if (_rules.Recipes[i].Id == recipe)
                    return i;
            }
            return -1;
        }

        private void ApplyActive()
        {
            _buffer.Clear();
            var active = State.Active;
            var recipe = active == null ? null : _rules.Recipe(active.Recipe);
            if (recipe != null)
            {
                var times = active!.Fine ? _rules.FineMultiplier : 1;
                foreach (var modifier in recipe.Modifiers)
                    _buffer.Add(modifier.WithValue(modifier.Value * times));
            }
            _layer.Set(_buffer);
        }
    }
}
