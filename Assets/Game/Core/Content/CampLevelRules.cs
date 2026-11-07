using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CampBranch
    {
        public CampBranch(string id, string nameRu, string nameEn, IReadOnlyList<PerkValue> perks)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Perks = perks;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<PerkValue> Perks { get; }
    }

    public sealed class CampNeighbor
    {
        public CampNeighbor(string a, string b, MuseumPerk perk, double value)
        {
            A = a;
            B = b;
            Perk = perk;
            Value = value;
        }

        public string A { get; }
        public string B { get; }
        public MuseumPerk Perk { get; }
        public double Value { get; }
    }

    public sealed class CampLevelDef
    {
        public CampLevelDef(string building, string guild, IReadOnlyList<string> materials, MuseumPerk perk, double perLevel, IReadOnlyList<CampBranch> branches)
        {
            Building = building;
            Guild = guild;
            Materials = materials;
            Perk = perk;
            PerLevel = perLevel;
            Branches = branches;
        }

        public IReadOnlyList<CampBranch> Branches { get; }

        public CampBranch? Branch(string id)
        {
            foreach (var branch in Branches)
            {
                if (branch.Id == id)
                    return branch;
            }
            return null;
        }

        public string Building { get; }
        public string Guild { get; }
        public IReadOnlyList<string> Materials { get; }
        public MuseumPerk Perk { get; }
        public double PerLevel { get; }
    }

    public sealed class CampLevelRules
    {
        public const string FileName = "camp_levels.json";
        public const int SupportedVersion = 1;

        private CampLevelRules(int maxLevel, IReadOnlyList<int> guildLevels, IReadOnlyList<IReadOnlyList<int>> amounts, IReadOnlyList<CampLevelDef> buildings)
        {
            MaxLevel = maxLevel;
            GuildLevels = guildLevels;
            Amounts = amounts;
            Buildings = buildings;
        }

        public int BranchLevel { get; private set; }
        public IReadOnlyList<int> ChangeAmounts { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<string> Street { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<CampNeighbor> Neighbors { get; private set; } = Array.Empty<CampNeighbor>();

        public int MaxLevel { get; }
        public IReadOnlyList<int> GuildLevels { get; }
        public IReadOnlyList<IReadOnlyList<int>> Amounts { get; }
        public IReadOnlyList<CampLevelDef> Buildings { get; }

        public CampLevelDef? Of(string building)
        {
            foreach (var def in Buildings)
            {
                if (def.Building == building)
                    return def;
            }
            return null;
        }

        public int GuildLevelFor(int level) => level >= 1 && level <= GuildLevels.Count ? GuildLevels[level - 1] : 0;

        public IReadOnlyList<int> AmountsFor(int level) => level >= 2 && level - 2 < Amounts.Count ? Amounts[level - 2] : Array.Empty<int>();

        public static CampLevelRules Load(string json, CampRules camp)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var maxLevel = root.Field("maxLevel").AsInt(2, 10);
            var guildLevels = new List<int>();
            foreach (var item in root.Field("guildLevels").Items())
                guildLevels.Add(item.AsInt(0, 10));
            if (guildLevels.Count != maxLevel)
                throw root.Field("guildLevels").Error($"give a guild level for each of {maxLevel} levels");
            var amounts = new List<IReadOnlyList<int>>();
            foreach (var row in root.Field("amounts").Items())
            {
                var amount = new List<int>();
                foreach (var item in row.Items())
                    amount.Add(item.AsInt(1, 10_000));
                amounts.Add(amount);
            }
            if (amounts.Count != maxLevel - 1)
                throw root.Field("amounts").Error($"give amounts for levels 2 to {maxLevel}");
            var buildings = new List<CampLevelDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("buildings").Items())
            {
                var building = node.Field("building").AsString();
                if (camp.Building(building) == null)
                    throw node.Field("building").Error($"unknown camp building '{building}'");
                if (!ids.Add(building))
                    throw node.Field("building").Error($"building '{building}' is listed twice");
                var materials = new List<string>();
                foreach (var item in node.Field("materials").Items())
                    materials.Add(item.AsString());
                foreach (var row in amounts)
                {
                    if (row.Count != materials.Count)
                        throw node.Field("materials").Error($"building '{building}' needs one amount per material on every level");
                }
                var perkName = node.Field("perk").AsString();
                if (!MuseumRules.TryPerk(perkName, out var perk))
                    throw node.Field("perk").Error($"unknown perk '{perkName}'");
                var perLevel = node.Field("perLevel").AsNumber(0.001, MuseumRules.MaxOf(perk));
                if (perLevel * (maxLevel - 1) > MuseumRules.MaxOf(perk))
                    throw node.Field("perLevel").Error($"building '{building}' gives more than {MuseumRules.MaxOf(perk)} at the top level");
                buildings.Add(new CampLevelDef(building, node.HasField("guild") ? node.Field("guild").AsString() : string.Empty, materials, perk, perLevel,
                    BranchesOf(node.Field("branches"))));
            }
            var branchLevel = root.Field("branchLevel").AsInt(2, maxLevel);
            var changeAmounts = new List<int>();
            foreach (var item in root.Field("changeAmounts").Items())
                changeAmounts.Add(item.AsInt(1, 10_000));
            foreach (var def in buildings)
            {
                if (changeAmounts.Count != def.Materials.Count)
                    throw root.Field("changeAmounts").Error($"a branch change needs one amount per material of '{def.Building}'");
            }
            var street = new List<string>();
            foreach (var item in root.Field("street").Items())
            {
                var id = item.AsString();
                if (!ids.Contains(id) || street.Contains(id))
                    throw item.Error($"street lists '{id}' that is not a levelled building or lists it twice");
                street.Add(id);
            }
            if (street.Count != buildings.Count)
                throw root.Field("street").Error("the street holds every levelled building once");
            var neighbors = new List<CampNeighbor>();
            foreach (var node in root.Field("neighbors").Items())
            {
                var a = node.Field("a").AsString();
                var b = node.Field("b").AsString();
                if (!ids.Contains(a) || !ids.Contains(b) || a == b)
                    throw node.Error($"neighbors '{a}' and '{b}' must be two different levelled buildings");
                var perkName = node.Field("perk").AsString();
                if (!MuseumRules.TryPerk(perkName, out var perk))
                    throw node.Field("perk").Error($"unknown perk '{perkName}'");
                neighbors.Add(new CampNeighbor(a, b, perk, node.Field("value").AsNumber(0.001, MuseumRules.MaxOf(perk))));
            }
            return new CampLevelRules(maxLevel, guildLevels, amounts, buildings) { BranchLevel = branchLevel, ChangeAmounts = changeAmounts, Street = street, Neighbors = neighbors };
        }

        private static IReadOnlyList<CampBranch> BranchesOf(ContentNode node)
        {
            var branches = new List<CampBranch>();
            foreach (var item in node.Items())
            {
                var perks = new List<PerkValue>();
                foreach (var line in item.Field("perks").Items())
                {
                    var perkName = line.Field("perk").AsString();
                    if (!MuseumRules.TryPerk(perkName, out var perk))
                        throw line.Field("perk").Error($"unknown perk '{perkName}'");
                    perks.Add(new PerkValue(perk, line.Field("value").AsNumber(0.001, MuseumRules.MaxOf(perk))));
                }
                var name = item.Field("name");
                branches.Add(new CampBranch(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), perks));
            }
            if (branches.Count != 2)
                throw node.Error("a building has exactly two branches");
            return branches;
        }
    }
}
