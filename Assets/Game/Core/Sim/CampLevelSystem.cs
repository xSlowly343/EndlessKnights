using System.Collections.Generic;
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
        NotEnough,
        LevelTooLow,
        NoSuchBranch,
        SameBranch
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
        public CampModuleSystem? Modules { get; set; }

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

        public string BranchOf(string building) => _state.Camp.Branches.TryGetValue(building, out var branch) ? branch : string.Empty;

        public CampLevelResult CheckBranch(string building, string branch)
        {
            var def = _rules.Of(building);
            if (def == null)
                return CampLevelResult.NotLevelled;
            if (def.Branch(branch) == null)
                return CampLevelResult.NoSuchBranch;
            if (Level(building) < _rules.BranchLevel)
                return CampLevelResult.LevelTooLow;
            var current = BranchOf(building);
            if (current == branch)
                return CampLevelResult.SameBranch;
            if (current.Length == 0)
                return CampLevelResult.Done;
            for (var i = 0; i < def.Materials.Count; i++)
            {
                if (_camp.Count(def.Materials[i]) < _rules.ChangeAmounts[i])
                    return CampLevelResult.NotEnough;
            }
            return CampLevelResult.Done;
        }

        public CampLevelResult Specialize(string building, string branch)
        {
            var result = CheckBranch(building, branch);
            if (result != CampLevelResult.Done)
                return result;
            if (BranchOf(building).Length > 0)
            {
                var def = _rules.Of(building)!;
                for (var i = 0; i < def.Materials.Count; i++)
                    _camp.Take(def.Materials[i], _rules.ChangeAmounts[i]);
            }
            _state.Camp.Branches[building] = branch;
            Apply();
            return CampLevelResult.Done;
        }

        public IReadOnlyList<string> Street
        {
            get
            {
                var street = _state.Camp.Street;
                if (street.Count != _rules.Street.Count || !SameSet(street, _rules.Street))
                {
                    street.Clear();
                    street.AddRange(_rules.Street);
                }
                return street;
            }
        }

        public bool Move(string building, int index)
        {
            var street = (List<string>)Street;
            var from = street.IndexOf(building);
            if (from < 0 || index < 0 || index >= street.Count)
                return false;
            street.RemoveAt(from);
            street.Insert(index, building);
            Apply();
            return true;
        }

        public bool Neighbors(string a, string b)
        {
            var street = Street;
            var i = -1;
            var j = -1;
            for (var k = 0; k < street.Count; k++)
            {
                if (street[k] == a)
                    i = k;
                if (street[k] == b)
                    j = k;
            }
            if (i < 0 || j < 0 || System.Math.Abs(i - j) != 1)
                return false;
            return Level(a) > 0 && Level(b) > 0;
        }

        private static bool SameSet(List<string> street, IReadOnlyList<string> rules)
        {
            foreach (var id in rules)
            {
                if (!street.Contains(id))
                    return false;
            }
            return true;
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
                var branch = def.Branch(BranchOf(def.Building));
                if (branch != null && level >= _rules.BranchLevel)
                {
                    foreach (var perk in branch.Perks)
                        perks[(int)perk.Perk] += perk.Value;
                }
            }
            foreach (var pair in _rules.Neighbors)
            {
                if (Neighbors(pair.A, pair.B))
                    perks[(int)pair.Perk] += pair.Value;
            }
            Modules?.AddPerks(perks);
        }
    }
}
