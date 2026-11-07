using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public readonly struct ModuleMaterial
    {
        public ModuleMaterial(string item, int count)
        {
            Item = item;
            Count = count;
        }

        public string Item { get; }
        public int Count { get; }
    }

    public sealed class CampModuleDef
    {
        public CampModuleDef(string id, string building, bool blueprint, string nameRu, string nameEn, PerkValue perk, IReadOnlyList<ModuleMaterial> materials)
        {
            Id = id;
            Building = building;
            Blueprint = blueprint;
            NameRu = nameRu;
            NameEn = nameEn;
            Perk = perk;
            Materials = materials;
        }

        public string Id { get; }
        public string Building { get; }
        public bool Blueprint { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public PerkValue Perk { get; }
        public IReadOnlyList<ModuleMaterial> Materials { get; }
    }

    public sealed class CampModuleRules
    {
        public const string FileName = "camp_modules.json";
        public const int SupportedVersion = 1;
        public const int MaxSockets = 3;

        public IReadOnlyList<int> Sockets { get; private set; } = Array.Empty<int>();
        public double BlueprintChance { get; private set; }
        public IReadOnlyList<CampModuleDef> Modules { get; private set; } = Array.Empty<CampModuleDef>();

        public CampModuleDef? Of(string id)
        {
            foreach (var def in Modules)
            {
                if (def.Id == id)
                    return def;
            }
            return null;
        }

        public int SocketsAt(int level) => level < 1 ? 0 : Sockets[System.Math.Min(level, Sockets.Count) - 1];

        public static CampModuleRules Load(string json, CampLevelRules levels)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var sockets = new List<int>();
            foreach (var item in root.Field("sockets").Items())
            {
                var count = item.AsInt(1, MaxSockets);
                if (sockets.Count > 0 && count < sockets[sockets.Count - 1])
                    throw item.Error("a higher level never has fewer sockets");
                sockets.Add(count);
            }
            if (sockets.Count != levels.MaxLevel)
                throw root.Field("sockets").Error($"give a socket count for each of {levels.MaxLevel} levels");
            var modules = new List<CampModuleDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("modules").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"module '{id}' is listed twice");
                var building = node.Field("building").AsString();
                if (levels.Of(building) == null)
                    throw node.Field("building").Error($"'{building}' is not a levelled camp building");
                var perkName = node.Field("perk").AsString();
                if (!MuseumRules.TryPerk(perkName, out var perk))
                    throw node.Field("perk").Error($"unknown perk '{perkName}'");
                var value = node.Field("value").AsNumber(0.001, MuseumRules.MaxOf(perk));
                var materials = new List<ModuleMaterial>();
                foreach (var item in node.Field("materials").Items())
                    materials.Add(new ModuleMaterial(item.Field("item").AsString(), item.Field("count").AsInt(1, 10_000)));
                if (materials.Count == 0)
                    throw node.Field("materials").Error($"module '{id}' needs materials");
                var name = node.Field("name");
                modules.Add(new CampModuleDef(id, building, node.HasField("blueprint") && node.Field("blueprint").AsBool(), name.Field("ru").AsString(), name.Field("en").AsString(),
                    new PerkValue(perk, value), materials));
            }
            return new CampModuleRules
            {
                Sockets = sockets,
                BlueprintChance = root.Field("blueprintChance").AsNumber(0, 1),
                Modules = modules
            };
        }
    }
}
