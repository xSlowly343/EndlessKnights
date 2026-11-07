using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class ApprenticeDef
    {
        public ApprenticeDef(string building, string guild, string nameRu, string nameEn, IReadOnlyList<string> items, string potion)
        {
            Building = building;
            Guild = guild;
            NameRu = nameRu;
            NameEn = nameEn;
            Items = items;
            Potion = potion;
        }

        public string Building { get; }
        public string Guild { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<string> Items { get; }
        public string Potion { get; }
    }

    public sealed class ApprenticeTrait
    {
        public ApprenticeTrait(string id, string nameRu, string nameEn, double speed, double twice)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Speed = speed;
            Twice = twice;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Speed { get; }
        public double Twice { get; }
    }

    public sealed class ApprenticeRules
    {
        public const string FileName = "apprentices.json";
        public const int SupportedVersion = 1;

        public int MinGuildLevel { get; private set; }
        public double SecondsPerItem { get; private set; }
        public double MaxOfflineSeconds { get; private set; }
        public int MaxPending { get; private set; }
        public IReadOnlyList<double> LevelHours { get; private set; } = Array.Empty<double>();
        public double SpeedPerLevel { get; private set; }
        public int TraitLevel { get; private set; }
        public IReadOnlyList<ApprenticeDef> Apprentices { get; private set; } = Array.Empty<ApprenticeDef>();
        public IReadOnlyList<ApprenticeTrait> Traits { get; private set; } = Array.Empty<ApprenticeTrait>();

        public ApprenticeDef? Of(string building)
        {
            foreach (var def in Apprentices)
            {
                if (def.Building == building)
                    return def;
            }
            return null;
        }

        public ApprenticeTrait? Trait(string id)
        {
            foreach (var trait in Traits)
            {
                if (trait.Id == id)
                    return trait;
            }
            return null;
        }

        public int LevelAt(double workedSeconds)
        {
            var level = 0;
            while (level < LevelHours.Count && workedSeconds >= LevelHours[level] * 3600)
                level++;
            return level;
        }

        public static ApprenticeRules Load(string json, CampRules camp, AlchemyRules alchemy)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var levels = new List<double>();
            foreach (var item in root.Field("levelHours").Items())
            {
                var hours = item.AsNumber(0, 100_000);
                if (levels.Count == 0 && hours != 0)
                    throw item.Error("level 1 starts at 0 hours");
                if (levels.Count > 0 && hours <= levels[levels.Count - 1])
                    throw item.Error("each level needs more hours than the one before");
                levels.Add(hours);
            }
            var apprentices = new List<ApprenticeDef>();
            foreach (var node in root.Field("apprentices").Items())
            {
                var building = node.Field("building").AsString();
                if (camp.Building(building) == null)
                    throw node.Field("building").Error($"unknown camp building '{building}'");
                foreach (var other in apprentices)
                {
                    if (other.Building == building)
                        throw node.Field("building").Error($"building '{building}' has two apprentices");
                }
                var items = new List<string>();
                if (node.HasField("items"))
                {
                    foreach (var item in node.Field("items").Items())
                        items.Add(item.AsString());
                }
                var potion = node.HasField("potion") ? node.Field("potion").AsString() : string.Empty;
                if (potion.Length > 0 && alchemy.Recipe(potion) == null)
                    throw node.Field("potion").Error($"unknown recipe '{potion}'");
                if ((items.Count > 0) == (potion.Length > 0))
                    throw node.Error($"apprentice of '{building}' brings either items or a potion");
                var name = node.Field("name");
                apprentices.Add(new ApprenticeDef(building, node.Field("guild").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), items, potion));
            }
            var traits = new List<ApprenticeTrait>();
            foreach (var node in root.Field("traits").Items())
            {
                var name = node.Field("name");
                traits.Add(new ApprenticeTrait(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                    node.Field("speed").AsNumber(0, 1), node.Field("double").AsNumber(0, 0.5)));
            }
            if (traits.Count == 0)
                throw root.Field("traits").Error("apprentices need traits");
            return new ApprenticeRules
            {
                MinGuildLevel = root.Field("minGuildLevel").AsInt(1, 10),
                SecondsPerItem = root.Field("secondsPerItem").AsNumber(60, 1_000_000),
                MaxOfflineSeconds = root.Field("maxOfflineHours").AsNumber(0, 168) * 3600,
                MaxPending = root.Field("maxPending").AsInt(1, 1000),
                LevelHours = levels,
                SpeedPerLevel = root.Field("speedPerLevel").AsNumber(0, 1),
                TraitLevel = root.Field("traitLevel").AsInt(1, levels.Count),
                Apprentices = apprentices,
                Traits = traits
            };
        }
    }
}
