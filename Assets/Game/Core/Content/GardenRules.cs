using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum GardenInput
    {
        Keys,
        Mouse,
        Clicks,
        Idle
    }

    public sealed class GardenPlant
    {
        public GardenPlant(string id, string nameRu, string nameEn, GardenInput input, bool starter, double growSeconds, double secondsPerUnit, string herb, int yield)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Input = input;
            Starter = starter;
            GrowSeconds = growSeconds;
            SecondsPerUnit = secondsPerUnit;
            Herb = herb;
            Yield = yield;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public GardenInput Input { get; }
        public bool Starter { get; }
        public double GrowSeconds { get; }
        public double SecondsPerUnit { get; }
        public string Herb { get; }
        public int Yield { get; }
        public string Seed => "seed_" + Id;
    }

    public readonly struct GardenMutation
    {
        public GardenMutation(string first, string second, string child, double chance)
        {
            First = first;
            Second = second;
            Child = child;
            Chance = chance;
        }

        public string First { get; }
        public string Second { get; }
        public string Child { get; }
        public double Chance { get; }

        public bool Matches(string a, string b) => (a == First && b == Second) || (a == Second && b == First);
    }

    public sealed class GardenRules
    {
        public const string FileName = "garden.json";
        public const int SupportedVersion = 1;

        private static readonly IReadOnlyDictionary<string, GardenInput> Inputs = new Dictionary<string, GardenInput>(StringComparer.Ordinal)
        {
            ["keys"] = GardenInput.Keys,
            ["mouse"] = GardenInput.Mouse,
            ["clicks"] = GardenInput.Clicks,
            ["idle"] = GardenInput.Idle
        };

        public IReadOnlyList<GardenPlant> Plants { get; private set; } = Array.Empty<GardenPlant>();
        public IReadOnlyList<GardenMutation> Mutations { get; private set; } = Array.Empty<GardenMutation>();
        public double SeedBackChance { get; private set; }

        public GardenPlant? Plant(string id)
        {
            for (var i = 0; i < Plants.Count; i++)
            {
                if (Plants[i].Id == id)
                    return Plants[i];
            }
            return null;
        }

        public static GardenRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var plants = new List<GardenPlant>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("plants").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"plant '{id}' is listed twice");
                var inputName = node.Field("input").AsString();
                if (!Inputs.TryGetValue(inputName, out var input))
                    throw node.Field("input").Error($"unknown input '{inputName}', expected keys, mouse, clicks or idle");
                var name = node.Field("name");
                plants.Add(new GardenPlant(id, name.Field("ru").AsString(), name.Field("en").AsString(), input,
                    node.HasField("starter") && node.Field("starter").AsBool(), node.Field("growMinutes").AsNumber(1, 100000) * 60,
                    node.Field("secondsPerUnit").AsNumber(0, 3600), node.Field("herb").AsString(), node.Field("yield").AsInt(1, 1000)));
            }
            var mutations = new List<GardenMutation>();
            foreach (var node in root.Field("mutations").Items())
            {
                var parents = new List<string>();
                foreach (var parent in node.Field("parents").Items())
                {
                    var id = parent.AsString();
                    if (!ids.Contains(id))
                        throw parent.Error($"unknown plant '{id}'");
                    parents.Add(id);
                }
                if (parents.Count != 2)
                    throw node.Field("parents").Error("a mutation needs two parents");
                var child = node.Field("child").AsString();
                if (!ids.Contains(child))
                    throw node.Field("child").Error($"unknown plant '{child}'");
                mutations.Add(new GardenMutation(parents[0], parents[1], child, node.Field("chance").AsNumber(0, 1)));
            }
            return new GardenRules { Plants = plants, Mutations = mutations, SeedBackChance = root.Field("seedBackChance").AsNumber(0, 1) };
        }
    }
}
