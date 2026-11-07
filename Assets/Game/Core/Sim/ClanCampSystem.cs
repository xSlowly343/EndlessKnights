using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum ClanCampResult
    {
        Done,
        NotAMaterial,
        BadAmount,
        NotEnough,
        NoSuchMeal
    }

    public sealed class ClanCampSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly ModifierLayer _layer;
        private ClanCampRules _rules;
        private bool _feasting;

        public ClanCampSystem(ClanCampRules rules, GameState state, StatSheet hero, CampSystem camp)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _layer = new ModifierLayer(hero);
        }

        public ClanCampRules Rules => _rules;
        public ClanCampState State => _state.ClanCamp;
        public bool Active { get; private set; }
        public bool Feasting => Active && _feasting;
        public IReadOnlyList<Modifier> Applied => _layer.Applied;

        public void UseRules(ClanCampRules rules)
        {
            _rules = rules;
            if (Active)
                Apply();
        }

        public void Use(int level, long feastUntilUnixMs, long nowUnixMs)
        {
            var camp = State;
            camp.Level = System.Math.Max(0, System.Math.Min(level, _rules.MaxLevel));
            camp.FeastUntilUnixMs = feastUntilUnixMs;
            camp.AnsweredUnixMs = nowUnixMs;
            camp.SeenUnixMs = nowUnixMs;
            Update(nowUnixMs, true);
        }

        public void Clear(long nowUnixMs) => Use(0, 0, nowUnixMs);

        public bool Refresh(long nowUnixMs) => Update(nowUnixMs, false);

        private bool Update(long nowUnixMs, bool rebuild)
        {
            var camp = State;
            if (nowUnixMs > camp.SeenUnixMs)
                camp.SeenUnixMs = nowUnixMs;
            var fresh = camp.Level > 0 && camp.SeenUnixMs - camp.AnsweredUnixMs <= _rules.OfflineHours * 3_600_000L;
            var feasting = fresh && camp.SeenUnixMs < camp.FeastUntilUnixMs;
            if (rebuild || fresh != Active || feasting != _feasting)
            {
                Active = fresh;
                _feasting = feasting;
                Apply();
            }
            return fresh;
        }

        public ClanCampResult PackMaterial(string item, int amount)
        {
            if (_rules.PointsOf(item) == 0)
                return ClanCampResult.NotAMaterial;
            if (amount <= 0 || amount > _rules.MaxGive)
                return ClanCampResult.BadAmount;
            return _camp.Take(item, amount) ? ClanCampResult.Done : ClanCampResult.NotEnough;
        }

        public ClanCampResult ReturnMaterial(string item, int amount)
        {
            if (_rules.PointsOf(item) == 0)
                return ClanCampResult.NotAMaterial;
            if (amount <= 0 || amount > _rules.MaxGive)
                return ClanCampResult.BadAmount;
            _camp.Add(item, amount);
            return ClanCampResult.Done;
        }

        public ClanCampResult PackMeal(int index, out string dish)
        {
            var meals = _state.Cooking.Meals;
            dish = string.Empty;
            if (index < 0 || index >= meals.Count)
                return ClanCampResult.NoSuchMeal;
            dish = meals[index];
            meals.RemoveAt(index);
            return ClanCampResult.Done;
        }

        public void ReturnMeal(string dish) => _state.Cooking.Meals.Add(dish);

        private void Apply()
        {
            var modifiers = new List<Modifier>();
            if (Active)
            {
                for (var i = 0; i < State.Level; i++)
                    modifiers.AddRange(_rules.Levels[i].Modifiers);
                if (_feasting)
                    modifiers.AddRange(_rules.Feast.Modifiers);
            }
            _layer.Set(modifiers);
        }
    }
}
