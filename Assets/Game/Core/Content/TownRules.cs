using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class TownResident
    {
        public TownResident(string id, string nameRu, string nameEn, string guild)
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

    public sealed class TownRequest
    {
        public TownRequest(string id, TownResident resident, string item, string recipe, string plant, string biome, int min, int max, string textRu, string textEn)
        {
            Id = id;
            Resident = resident;
            Item = item;
            Recipe = recipe;
            Plant = plant;
            Biome = biome;
            Min = min;
            Max = max;
            TextRu = textRu;
            TextEn = textEn;
        }

        public string Id { get; }
        public TownResident Resident { get; }
        public string Item { get; }
        public string Recipe { get; }
        public string Plant { get; }
        public string Biome { get; }
        public int Min { get; }
        public int Max { get; }
        public string TextRu { get; }
        public string TextEn { get; }
        public bool WantsPotion => Recipe.Length > 0;
    }

    public sealed class TownRules
    {
        public const string FileName = "town.json";
        public const int SupportedVersion = 1;

        public int OrdersPerDay { get; private set; }
        public int OrdersKept { get; private set; }
        public int OrderDays { get; private set; }
        public long Reputation { get; private set; }
        public long Shards { get; private set; }
        public double Dust { get; private set; }
        public double RecipeChance { get; private set; }
        public IReadOnlyList<TownResident> Residents { get; private set; } = Array.Empty<TownResident>();
        public IReadOnlyList<TownRequest> Requests { get; private set; } = Array.Empty<TownRequest>();
        public int GiftsPerDay { get; private set; }
        public int GiftMaxAmount { get; private set; }
        public int GiftInbox { get; private set; }
        public IReadOnlyList<string> GiftItems { get; private set; } = Array.Empty<string>();

        public TownRequest? Request(string id)
        {
            for (var i = 0; i < Requests.Count; i++)
            {
                if (Requests[i].Id == id)
                    return Requests[i];
            }
            return null;
        }

        public bool Giftable(string item)
        {
            for (var i = 0; i < GiftItems.Count; i++)
            {
                if (GiftItems[i] == item)
                    return true;
            }
            return false;
        }

        public static TownRules Load(string json, AlchemyRules alchemy, GardenRules garden, IReadOnlyList<string> biomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var orders = root.Field("orders");
            var reward = root.Field("reward");
            var gifts = root.Field("gifts");
            var residents = new Dictionary<string, TownResident>(StringComparer.Ordinal);
            var residentList = new List<TownResident>();
            foreach (var node in root.Field("residents").Items())
            {
                var name = node.Field("name");
                var resident = new TownResident(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), node.Field("guild").AsString());
                residents[resident.Id] = resident;
                residentList.Add(resident);
            }
            var requests = new List<TownRequest>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("requests").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"duplicate request '{id}'");
                var residentId = node.Field("resident").AsString();
                if (!residents.TryGetValue(residentId, out var resident))
                    throw node.Field("resident").Error($"unknown resident '{residentId}'");
                var item = Optional(node, "item");
                var recipe = Optional(node, "recipe");
                if ((item.Length > 0) == (recipe.Length > 0))
                    throw node.Error($"request '{id}' needs either an item or a recipe");
                if (recipe.Length > 0 && alchemy.Recipe(recipe) == null)
                    throw node.Field("recipe").Error($"unknown recipe '{recipe}'");
                var plant = Optional(node, "plant");
                if (plant.Length > 0 && garden.Plant(plant) == null)
                    throw node.Field("plant").Error($"unknown plant '{plant}'");
                var biome = Optional(node, "biome");
                if (biome.Length > 0 && !Contains(biomes, biome))
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                var min = node.Field("min").AsInt(1, 1000);
                var max = node.Field("max").AsInt(min, 1000);
                var text = node.Field("text");
                requests.Add(new TownRequest(id, resident, item, recipe, plant, biome, min, max, text.Field("ru").AsString(), text.Field("en").AsString()));
            }
            var giftItems = new List<string>();
            foreach (var node in gifts.Field("items").Items())
                giftItems.Add(node.AsString());
            return new TownRules
            {
                OrdersPerDay = orders.Field("perDay").AsInt(1, 20),
                OrdersKept = orders.Field("keep").AsInt(1, 50),
                OrderDays = orders.Field("days").AsInt(1, 365),
                Reputation = reward.Field("reputation").AsLong(0, 1_000_000),
                Shards = reward.Field("shards").AsLong(0, 1_000_000),
                Dust = reward.Field("dust").AsNumber(0, 1_000_000),
                RecipeChance = reward.Field("recipeChance").AsNumber(0, 1),
                Residents = residentList,
                Requests = requests,
                GiftsPerDay = gifts.Field("perDay").AsInt(0, 100),
                GiftMaxAmount = gifts.Field("maxAmount").AsInt(1, 1000),
                GiftInbox = gifts.Field("inbox").AsInt(1, 1000),
                GiftItems = giftItems
            };
        }

        private static string Optional(ContentNode node, string name) => node.HasField(name) ? node.Field(name).AsString() : string.Empty;

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }
}
