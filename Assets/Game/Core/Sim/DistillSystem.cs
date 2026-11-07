using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum DistillResult
    {
        Done,
        Closed,
        Busy,
        UnknownRecipe,
        NoHerbs,
        NoFuel,
        NoBloom
    }

    public sealed class DistillSystem
    {
        public const string Activity = "distill";

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly AlchemySystem _alchemy;
        private readonly ColossusSeedRules _colossus;
        private readonly EventQueue _events;
        private DistillRules _rules;

        public DistillSystem(DistillRules rules, ColossusSeedRules colossus, GameState state, CampSystem camp, PeacefulSystem peaceful, AlchemySystem alchemy, EventQueue events)
        {
            _rules = rules;
            _colossus = colossus;
            _state = state;
            _camp = camp;
            _peaceful = peaceful;
            _alchemy = alchemy;
            _events = events;
        }

        public DistillRules Rules => _rules;
        public DistillState State => _state.Distill;
        public bool Open => _camp.Open(_rules.Building) && (_rules.Guild.Length == 0 || _camp.State.Guilds.Contains(_rules.Guild));
        public bool Busy => State.Recipe.Length > 0;
        public double PressureRate => _rules.PressurePerSecond * System.Math.Max(0.1, 1 - _state.Perk(MuseumPerk.SlowerPressure));

        public void UseRules(DistillRules rules) => _rules = rules;

        public DistillResult Check(string recipeId, bool bloom = false)
        {
            if (!Open)
                return DistillResult.Closed;
            if (Busy)
                return DistillResult.Busy;
            var recipe = _alchemy.Rules.Recipe(recipeId);
            if (recipe == null || !_alchemy.Knows(recipeId))
                return DistillResult.UnknownRecipe;
            foreach (var herb in recipe.Herbs)
            {
                if (_camp.Count(herb) < Needed(recipe, herb))
                    return DistillResult.NoHerbs;
            }
            if (_camp.Count(_rules.Fuel.Item) < _rules.Fuel.Count)
                return DistillResult.NoFuel;
            if (bloom && _camp.Count(_colossus.Bloom) < 1)
                return DistillResult.NoBloom;
            return DistillResult.Done;
        }

        public DistillResult Start(string recipeId, bool bloom = false)
        {
            var result = Check(recipeId, bloom);
            if (result != DistillResult.Done)
                return result;
            foreach (var herb in _alchemy.Rules.Recipe(recipeId)!.Herbs)
                _camp.Take(herb, 1);
            _camp.Take(_rules.Fuel.Item, _rules.Fuel.Count);
            if (bloom)
                _camp.Take(_colossus.Bloom, 1);
            State.Recipe = recipeId;
            State.Bloom = bloom;
            State.Heat = 0;
            State.Pressure = 0;
            return DistillResult.Done;
        }

        public void Tick()
        {
            if (!Busy || !_peaceful.InCamp || _peaceful.Activity != Activity)
                return;
            var work = _peaceful.Work(Activity);
            var meters = System.Math.Max(0, work.MouseMeters);
            var clicks = work.Clicks;
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            State.Heat += meters;
            State.Pressure = System.Math.Max(0, State.Pressure + PressureRate * GameLoop.TickSeconds - clicks * _rules.Vent);
            if (State.Pressure >= 1)
                Burst();
            else if (State.Heat >= _rules.HeatMeters)
                Finish();
        }

        private static int Needed(PotionRecipe recipe, string herb)
        {
            var count = 0;
            foreach (var item in recipe.Herbs)
            {
                if (item == herb)
                    count++;
            }
            return count;
        }

        private void Finish()
        {
            var chance = _rules.MiracleChance + (State.Pressure >= _rules.Edge ? _rules.EdgeMiracleChance : 0);
            var miracle = State.Bloom || _state.Rng.NextDouble() < chance;
            Bottle(miracle);
            State.Done++;
            _events.Add(GameEvent.ForStage(GameEventKind.TinctureDistilled, _state.Tick, miracle ? 1 : 0));
            Reset();
        }

        private void Burst()
        {
            State.Bursts++;
            var miracle = _state.Rng.NextDouble() < _rules.BurstMiracleChance;
            if (miracle)
                Bottle(true);
            _events.Add(GameEvent.ForStage(GameEventKind.TinctureBurst, _state.Tick, miracle ? 1 : 0));
            Reset();
        }

        private void Bottle(bool miracle)
        {
            if (miracle)
                State.Miracles++;
            _alchemy.State.Potions.Add(new Potion { Recipe = State.Recipe, Fine = miracle, Lasting = miracle ? _rules.MiracleLasting : _rules.TinctureLasting });
        }

        private void Reset()
        {
            State.Recipe = string.Empty;
            State.Bloom = false;
            State.Heat = 0;
            State.Pressure = 0;
        }
    }
}
