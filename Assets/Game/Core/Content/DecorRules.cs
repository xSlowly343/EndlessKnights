using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum MonumentInput
    {
        MouseKm,
        Keys,
        Clicks
    }

    public sealed class MonumentDef
    {
        public MonumentDef(string id, string nameRu, string nameEn, MonumentInput input, IReadOnlyList<double> steps)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Input = input;
            Steps = steps;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public MonumentInput Input { get; }
        public IReadOnlyList<double> Steps { get; }
    }

    public sealed class DecorDef
    {
        public DecorDef(string id, string nameRu, string nameEn, bool start, IReadOnlyDictionary<string, int> cost)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Start = start;
            Cost = cost;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public bool Start { get; }
        public IReadOnlyDictionary<string, int> Cost { get; }
    }

    public sealed class DecorRules
    {
        public const string FileName = "decor.json";
        public const int SupportedVersion = 1;

        private static readonly IReadOnlyDictionary<string, MonumentInput> Inputs = new Dictionary<string, MonumentInput>(StringComparer.Ordinal)
        {
            ["mouseKm"] = MonumentInput.MouseKm,
            ["keys"] = MonumentInput.Keys,
            ["clicks"] = MonumentInput.Clicks
        };

        private readonly Dictionary<string, int> _spots = new Dictionary<string, int>(StringComparer.Ordinal);

        public double WaterSeconds { get; private set; }
        public int GuestHammers { get; private set; }
        public int MostSpots { get; private set; }
        public IReadOnlyList<MonumentDef> Monuments { get; private set; } = Array.Empty<MonumentDef>();
        public IReadOnlyList<DecorDef> Items { get; private set; } = Array.Empty<DecorDef>();

        public int SpotsOf(string tier) => _spots.TryGetValue(tier, out var spots) ? spots : 0;

        public DecorDef? Item(string id)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id == id)
                    return Items[i];
            }
            return null;
        }

        public static DecorRules Load(string json, CampRules camp)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var guests = root.Field("guests");
            var rules = new DecorRules
            {
                WaterSeconds = guests.Field("waterMinutes").AsNumber(0, 1440) * 60,
                GuestHammers = guests.Field("hammers").AsInt(0, 1000)
            };
            var spots = root.Field("spots");
            foreach (var tier in camp.Tiers)
            {
                if (!spots.HasField(tier.Id))
                    throw spots.Error($"camp tier '{tier.Id}' has no decor spots");
                var count = spots.Field(tier.Id).AsInt(0, 100);
                rules._spots[tier.Id] = count;
                rules.MostSpots = System.Math.Max(rules.MostSpots, count);
            }
            var monuments = new List<MonumentDef>();
            foreach (var node in root.Field("monuments").Items())
            {
                var inputName = node.Field("input").AsString();
                if (!Inputs.TryGetValue(inputName, out var input))
                    throw node.Field("input").Error($"unknown monument input '{inputName}'");
                var steps = new List<double>();
                foreach (var step in node.Field("steps").Items())
                {
                    var value = step.AsNumber(0, 1e15);
                    if (steps.Count > 0 && value <= steps[steps.Count - 1])
                        throw step.Error("monument steps must grow");
                    steps.Add(value);
                }
                var name = node.Field("name");
                monuments.Add(new MonumentDef(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), input, steps));
            }
            rules.Monuments = monuments;
            var items = new List<DecorDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("items").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"decor '{id}' is listed twice");
                var start = node.HasField("start") && node.Field("start").AsBool();
                var cost = new Dictionary<string, int>(StringComparer.Ordinal);
                if (node.HasField("cost"))
                {
                    var costNode = node.Field("cost");
                    foreach (var key in costNode.FieldNames())
                    {
                        if (key.StartsWith("ore_", StringComparison.Ordinal))
                            throw costNode.Field(key).Error("ore is only for the forge, not for decor");
                        cost[key] = costNode.Field(key).AsInt(1, 1000);
                    }
                }
                if (!start && cost.Count == 0)
                    throw node.Error($"decor '{id}' is neither a starter nor craftable");
                var name = node.Field("name");
                items.Add(new DecorDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), start, cost));
            }
            rules.Items = items;
            return rules;
        }
    }
}
