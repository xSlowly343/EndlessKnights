using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class HideoutTier
    {
        public HideoutTier(string id, string nameRu, string nameEn, int members, long bosses, int orders, double sitHours, int fireLevel, IReadOnlyList<string> opens)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Members = members;
            Bosses = bosses;
            Orders = orders;
            SitHours = sitHours;
            FireLevel = fireLevel;
            Opens = opens;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int Members { get; }
        public long Bosses { get; }
        public int Orders { get; }
        public double SitHours { get; }
        public int FireLevel { get; }
        public IReadOnlyList<string> Opens { get; }
    }

    public sealed class HideoutBuilding
    {
        public HideoutBuilding(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class HideoutRules
    {
        public const string FileName = "party_hideout.json";
        public const int SupportedVersion = 1;

        private HideoutRules(int presenceSeconds, IReadOnlyList<HideoutTier> tiers, IReadOnlyList<HideoutBuilding> buildings)
        {
            PresenceSeconds = presenceSeconds;
            Tiers = tiers;
            Buildings = buildings;
        }

        public int PresenceSeconds { get; }
        public IReadOnlyList<HideoutTier> Tiers { get; }
        public IReadOnlyList<HideoutBuilding> Buildings { get; }

        public HideoutBuilding? Building(string id)
        {
            foreach (var building in Buildings)
            {
                if (building.Id == id)
                    return building;
            }
            return null;
        }

        public static HideoutRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var presence = root.Field("presenceSeconds").AsInt(10, 600);
            var here = root.Field("hereSeconds").AsInt(presence + 1, 3600);
            root.Field("maxSitStepSeconds").AsInt(presence, here * 10);
            var buildings = new List<HideoutBuilding>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("buildings").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"building '{id}' is listed twice");
                var name = node.Field("name");
                buildings.Add(new HideoutBuilding(id, name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            var tiers = new List<HideoutTier>();
            foreach (var node in root.Field("tiers").Items())
            {
                var opens = new List<string>();
                foreach (var item in node.Field("opens").Items())
                {
                    var building = item.AsString();
                    if (!ids.Contains(building))
                        throw item.Error($"unknown building '{building}'");
                    opens.Add(building);
                }
                var bosses = node.Field("bosses").AsLong(0);
                if (tiers.Count > 0 && bosses < tiers[tiers.Count - 1].Bosses)
                    throw node.Field("bosses").Error("a higher tier never needs fewer bosses");
                var name = node.Field("name");
                tiers.Add(new HideoutTier(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), node.Field("members").AsInt(1, 4),
                    bosses, node.Field("orders").AsInt(0, 1000), node.Field("sitHours").AsNumber(0, 10_000), node.Field("fireLevel").AsInt(0, 3), opens));
            }
            if (tiers.Count == 0)
                throw root.Field("tiers").Error("the hideout needs tiers");
            return new HideoutRules(presence, tiers, buildings);
        }
    }
}
