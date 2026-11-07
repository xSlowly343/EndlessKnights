using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum CampLevelResult
    {
        Done,
        NotLevelled,
        NotOpen,
        TopLevel,
        GuildTooLow,
        NotEnough
    }

    public sealed class CampLevelSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly GuildSystem _guilds;
        private CampLevelRules _rules;

        public CampLevelSystem(CampLevelRules rules, GameState state, CampSystem camp, GuildSystem guilds)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _guilds = guilds;
            Apply();
        }

        public CampLevelRules Rules => _rules;

        public void UseRules(CampLevelRules rules)
        {
            _rules = rules;
            Apply();
        }

        public int Level(string building)
        {
            if (!_camp.Open(building))
                return 0;
            return _state.Camp.Levels.TryGetValue(building, out var level) ? System.Math.Max(1, level) : 1;
        }

        public CampLevelResult Check(string building)
        {
            var def = _rules.Of(building);
            if (def == null)
                return CampLevelResult.NotLevelled;
            var level = Level(building);
            if (level == 0)
                return CampLevelResult.NotOpen;
            if (level >= _rules.MaxLevel)
                return CampLevelResult.TopLevel;
            var need = _rules.GuildLevelFor(level + 1);
            if (need > 0 && def.Guild.Length > 0 && _guilds.Level(def.Guild) < need)
                return CampLevelResult.GuildTooLow;
            var amounts = _rules.AmountsFor(level + 1);
            for (var i = 0; i < def.Materials.Count; i++)
            {
                if (_camp.Count(def.Materials[i]) < amounts[i])
                    return CampLevelResult.NotEnough;
            }
            return CampLevelResult.Done;
        }

        public CampLevelResult Upgrade(string building)
        {
            var result = Check(building);
            if (result != CampLevelResult.Done)
                return result;
            var def = _rules.Of(building)!;
            var level = Level(building);
            var amounts = _rules.AmountsFor(level + 1);
            for (var i = 0; i < def.Materials.Count; i++)
                _camp.Take(def.Materials[i], amounts[i]);
            _state.Camp.Levels[building] = level + 1;
            Apply();
            return CampLevelResult.Done;
        }

        public void Apply()
        {
            var perks = _state.CampPerks;
            for (var i = 0; i < perks.Length; i++)
                perks[i] = 0;
            foreach (var def in _rules.Buildings)
            {
                var level = _state.Camp.Levels.TryGetValue(def.Building, out var stored) ? stored : 1;
                if (level > 1)
                    perks[(int)def.Perk] += def.PerLevel * (level - 1);
            }
        }
    }
}
