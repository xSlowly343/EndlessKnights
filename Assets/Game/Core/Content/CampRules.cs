using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CampBuilding
    {
        public CampBuilding(string id, string nameRu, string nameEn, string guild)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Guild = guild;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Guild { get; }
    }

    public sealed class CampTier
    {
        public CampTier(string id, string nameRu, string nameEn, long fromRecord, IReadOnlyDictionary<string, int> slots)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            FromRecord = fromRecord;
            Slots = slots;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long FromRecord { get; }
        public IReadOnlyDictionary<string, int> Slots { get; }

        public int SlotsOf(string building) => Slots.TryGetValue(building, out var count) ? count : 0;
    }

    public sealed class CampRules
    {
        public const string FileName = "camp.json";
        public const int SupportedVersion = 1;

        public IReadOnlyList<CampBuilding> Buildings { get; private set; } = Array.Empty<CampBuilding>();
        public IReadOnlyList<CampTier> Tiers { get; private set; } = Array.Empty<CampTier>();

        public int TierAt(long record)
        {
            var tier = 0;
            for (var i = 0; i < Tiers.Count; i++)
            {
                if (record >= Tiers[i].FromRecord)
                    tier = i;
            }
            return tier;
        }

        public CampBuilding? Building(string id)
        {
            for (var i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i].Id == id)
                    return Buildings[i];
            }
            return null;
        }

        public static CampRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var buildings = new List<CampBuilding>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("buildings").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"building '{id}' is listed twice");
                var name = node.Field("name");
                buildings.Add(new CampBuilding(id, name.Field("ru").AsString(), name.Field("en").AsString(),
                    node.HasField("guild") ? node.Field("guild").AsString() : string.Empty));
            }
            var tiers = new List<CampTier>();
            long previous = 0;
            foreach (var node in root.Field("tiers").Items())
            {
                var from = node.Field("fromRecord").AsLong(1);
                if (tiers.Count == 0 && from != 1)
                    throw node.Field("fromRecord").Error("the first tier starts at record 1");
                if (from <= previous)
                    throw node.Field("fromRecord").Error("tiers must go up by record");
                previous = from;
                var slots = new Dictionary<string, int>(StringComparer.Ordinal);
                var slotsNode = node.Field("slots");
                foreach (var building in slotsNode.FieldNames())
                {
                    if (!ids.Contains(building))
                        throw slotsNode.Field(building).Error($"unknown building '{building}'");
                    slots[building] = slotsNode.Field(building).AsInt(0, 100);
                }
                var name = node.Field("name");
                tiers.Add(new CampTier(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), from, slots));
            }
            if (tiers.Count == 0)
                throw root.Field("tiers").Error("at least one tier is required");
            return new CampRules { Buildings = buildings, Tiers = tiers };
        }
    }
}
