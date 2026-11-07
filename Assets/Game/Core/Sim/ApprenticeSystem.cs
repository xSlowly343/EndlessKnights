using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum ApprenticeResult
    {
        Done,
        NoApprentice,
        NotOpen,
        GuildTooLow,
        AlreadyHired
    }

    public sealed class ApprenticeSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly GuildSystem _guilds;
        private ApprenticeRules _rules;

        public ApprenticeSystem(ApprenticeRules rules, GameState state, CampSystem camp, GuildSystem guilds)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _guilds = guilds;
        }

        public ApprenticeRules Rules => _rules;
        public List<ApprenticeState> Hired => _state.Apprentices;

        public void UseRules(ApprenticeRules rules) => _rules = rules;

        public ApprenticeState? At(string building)
        {
            foreach (var apprentice in _state.Apprentices)
            {
                if (apprentice.Building == building)
                    return apprentice;
            }
            return null;
        }

        public int Level(ApprenticeState apprentice) => _rules.LevelAt(apprentice.Worked);

        public ApprenticeResult Check(string building)
        {
            var def = _rules.Of(building);
            if (def == null)
                return ApprenticeResult.NoApprentice;
            if (At(building) != null)
                return ApprenticeResult.AlreadyHired;
            if (!_camp.Open(building))
                return ApprenticeResult.NotOpen;
            if (_guilds.Level(def.Guild) < _rules.MinGuildLevel)
                return ApprenticeResult.GuildTooLow;
            return ApprenticeResult.Done;
        }

        public ApprenticeResult Hire(string building)
        {
            var result = Check(building);
            if (result == ApprenticeResult.Done)
                _state.Apprentices.Add(new ApprenticeState { Building = building });
            return result;
        }

        public void BattleTick() => Work(GameLoop.TickSeconds);

        public void AdvanceOffline(double seconds)
        {
            Work(System.Math.Min(System.Math.Max(0, seconds), _rules.MaxOfflineSeconds));
            Collect();
        }

        public double Speed(ApprenticeState apprentice)
        {
            var trait = _rules.Trait(apprentice.Trait);
            return 1 + _rules.SpeedPerLevel * (Level(apprentice) - 1) + (trait?.Speed ?? 0);
        }

        public int Pending(ApprenticeState apprentice) => (int)(apprentice.Progress / _rules.SecondsPerItem);

        public void Collect()
        {
            foreach (var apprentice in _state.Apprentices)
            {
                var def = _rules.Of(apprentice.Building);
                if (def == null)
                    continue;
                if (apprentice.Trait.Length == 0 && Level(apprentice) >= _rules.TraitLevel)
                    apprentice.Trait = _rules.Traits[_state.Rng.NextInt(0, _rules.Traits.Count)].Id;
                var twice = _rules.Trait(apprentice.Trait)?.Twice ?? 0;
                while (apprentice.Progress >= _rules.SecondsPerItem)
                {
                    apprentice.Progress -= _rules.SecondsPerItem;
                    Bring(def, apprentice);
                    if (_state.Rng.NextDouble() < twice)
                        Bring(def, apprentice);
                }
            }
        }

        private void Work(double seconds)
        {
            var cap = _rules.SecondsPerItem * _rules.MaxPending;
            var apprentices = _state.Apprentices;
            for (var i = 0; i < apprentices.Count; i++)
            {
                var apprentice = apprentices[i];
                apprentice.Worked += seconds;
                apprentice.Progress = System.Math.Min(cap, apprentice.Progress + seconds * Speed(apprentice));
            }
        }

        private void Bring(ApprenticeDef def, ApprenticeState apprentice)
        {
            apprentice.Brought++;
            if (def.Potion.Length > 0)
                _state.Alchemy.Potions.Add(new Potion { Recipe = def.Potion });
            else
                _camp.Add(def.Items[_state.Rng.NextInt(0, def.Items.Count)], 1);
        }
    }
}
