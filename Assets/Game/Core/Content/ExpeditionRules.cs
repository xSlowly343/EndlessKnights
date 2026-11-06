using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CompanionTrait
    {
        public CompanionTrait(string id, string nameRu, string nameEn, double itemMultiplier, double shardsMultiplier, double rareMultiplier)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            ItemMultiplier = itemMultiplier;
            ShardsMultiplier = shardsMultiplier;
            RareMultiplier = rareMultiplier;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double ItemMultiplier { get; }
        public double ShardsMultiplier { get; }
        public double RareMultiplier { get; }
    }

    public sealed class ExpeditionCompanion
    {
        public ExpeditionCompanion(string id, string nameRu, string nameEn, string trait)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Trait = trait;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Trait { get; }
    }

    public readonly struct ExpeditionItem
    {
        public ExpeditionItem(string item, double perHour)
        {
            Item = item;
            PerHour = perHour;
        }

        public string Item { get; }
        public double PerHour { get; }
    }

    public sealed class ExpeditionBiome
    {
        public ExpeditionBiome(string biome, IReadOnlyList<ExpeditionItem> items, double shardsPerHour, double rareChancePerHour, string rare)
        {
            Biome = biome;
            Items = items;
            ShardsPerHour = shardsPerHour;
            RareChancePerHour = rareChancePerHour;
            Rare = rare;
        }

        public string Biome { get; }
        public IReadOnlyList<ExpeditionItem> Items { get; }
        public double ShardsPerHour { get; }
        public double RareChancePerHour { get; }
        public string Rare { get; }
    }

    public sealed class ExpeditionRules
    {
        public const string FileName = "expeditions.json";
        public const int SupportedVersion = 1;

        public IReadOnlyList<int> Hours { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<ExpeditionCompanion> Companions { get; private set; } = Array.Empty<ExpeditionCompanion>();
        public string CosmeticTrait { get; private set; } = string.Empty;
        public IReadOnlyList<CompanionTrait> Traits { get; private set; } = Array.Empty<CompanionTrait>();
        public double WaitExtend { get; private set; }
        public double WaitRareMultiplier { get; private set; }
        public double ChoiceChance { get; private set; }
        public IReadOnlyList<ExpeditionBiome> Biomes { get; private set; } = Array.Empty<ExpeditionBiome>();
        public IReadOnlyList<(string Ru, string En)> Letters { get; private set; } = Array.Empty<(string, string)>();

        public CompanionTrait? Trait(string id)
        {
            for (var i = 0; i < Traits.Count; i++)
            {
                if (Traits[i].Id == id)
                    return Traits[i];
            }
            return null;
        }

        public ExpeditionBiome? Biome(string biome)
        {
            for (var i = 0; i < Biomes.Count; i++)
            {
                if (Biomes[i].Biome == biome)
                    return Biomes[i];
            }
            return null;
        }

        public static ExpeditionRules Load(string json, IReadOnlyList<string> knownBiomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var hours = new List<int>();
            foreach (var item in root.Field("hours").Items())
                hours.Add(item.AsInt(1, 48));
            var traits = new List<CompanionTrait>();
            foreach (var node in root.Field("traits").Items())
            {
                var name = node.Field("name");
                traits.Add(new CompanionTrait(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                    node.Field("itemMultiplier").AsNumber(0, 10), node.Field("shardsMultiplier").AsNumber(0, 10), node.Field("rareMultiplier").AsNumber(0, 10)));
            }
            var rules = new ExpeditionRules { Traits = traits };
            var companions = new List<ExpeditionCompanion>();
            foreach (var node in root.Field("companions").Items())
            {
                var trait = node.Field("trait").AsString();
                if (rules.Trait(trait) == null)
                    throw node.Field("trait").Error($"unknown trait '{trait}'");
                var name = node.Field("name");
                companions.Add(new ExpeditionCompanion(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), trait));
            }
            var cosmeticTrait = root.Field("cosmeticTrait").AsString();
            if (rules.Trait(cosmeticTrait) == null)
                throw root.Field("cosmeticTrait").Error($"unknown trait '{cosmeticTrait}'");
            var biomes = new List<ExpeditionBiome>();
            foreach (var node in root.Field("biomes").Items())
            {
                var biome = node.Field("biome").AsString();
                if (!((ICollection<string>)new List<string>(knownBiomes)).Contains(biome))
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                var items = new List<ExpeditionItem>();
                foreach (var item in node.Field("items").Items())
                    items.Add(new ExpeditionItem(item.Field("item").AsString(), item.Field("perHour").AsNumber(0, 1000)));
                biomes.Add(new ExpeditionBiome(biome, items, node.Field("shardsPerHour").AsNumber(0, 1e6), node.Field("rareChancePerHour").AsNumber(0, 1), node.Field("rare").AsString()));
            }
            var letters = new List<(string, string)>();
            foreach (var node in root.Field("letters").Items())
                letters.Add((node.Field("ru").AsString(), node.Field("en").AsString()));
            return new ExpeditionRules
            {
                Hours = hours,
                Companions = companions,
                CosmeticTrait = cosmeticTrait,
                Traits = traits,
                WaitExtend = root.Field("waitExtend").AsNumber(0, 10),
                WaitRareMultiplier = root.Field("waitRareMultiplier").AsNumber(1, 10),
                ChoiceChance = root.Field("choiceChance").AsNumber(0, 1),
                Biomes = biomes,
                Letters = letters
            };
        }
    }
}
