using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum SpiritTrigger
    {
        Keys,
        MousePx,
        Clicks,
        Casts,
        IdleSeconds,
        Kills,
        Crits,
        Seconds
    }

    public sealed class SpiritDef
    {
        public SpiritDef(string id, string nameRu, string nameEn, SpiritTrigger trigger, double every, double multiplier, double perLevel, int maxLevel,
            double awakenCost, ExponentialCurve levelCost)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Trigger = trigger;
            Every = every;
            Multiplier = multiplier;
            PerLevel = perLevel;
            MaxLevel = maxLevel;
            AwakenCost = awakenCost;
            LevelCost = levelCost;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public SpiritTrigger Trigger { get; }
        public double Every { get; }
        public double Multiplier { get; }
        public double PerLevel { get; }
        public int MaxLevel { get; }
        public double AwakenCost { get; }
        public ExponentialCurve LevelCost { get; }

        public double MultiplierAt(int level) => level <= 0 ? 0 : Multiplier + PerLevel * (level - 1);
    }

    public sealed class SpiritCatalog
    {
        public const string FileName = "spirits.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, SpiritTrigger> Triggers = new Dictionary<string, SpiritTrigger>(StringComparer.Ordinal)
        {
            ["keys"] = SpiritTrigger.Keys,
            ["mousePx"] = SpiritTrigger.MousePx,
            ["clicks"] = SpiritTrigger.Clicks,
            ["casts"] = SpiritTrigger.Casts,
            ["idleSeconds"] = SpiritTrigger.IdleSeconds,
            ["kills"] = SpiritTrigger.Kills,
            ["crits"] = SpiritTrigger.Crits,
            ["seconds"] = SpiritTrigger.Seconds
        };

        private readonly Dictionary<string, int> _index;

        private SpiritCatalog(List<SpiritDef> all)
        {
            All = all;
            _index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < all.Count; i++)
                _index.Add(all[i].Id, i);
        }

        public IReadOnlyList<SpiritDef> All { get; }

        public bool TryGet(string id, out SpiritDef spirit)
        {
            spirit = null!;
            if (!_index.TryGetValue(id, out var index))
                return false;
            spirit = All[index];
            return true;
        }

        public static SpiritCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var all = new List<SpiritDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("spirits").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"spirit '{id}' is listed twice");
                var trigger = item.Field("trigger").AsString();
                if (!Triggers.TryGetValue(trigger, out var kind))
                    throw item.Field("trigger").Error($"unknown spirit trigger '{trigger}', expected one of {string.Join(", ", Triggers.Keys)}");
                var name = item.Field("name");
                all.Add(new SpiritDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), kind,
                    item.Field("every").AsNumber(0.1, 1e9),
                    item.Field("multiplier").AsNumber(0, 1e6),
                    item.Field("perLevel").AsNumber(0, 1e6),
                    item.Field("maxLevel").AsInt(1, 1000),
                    item.Field("awakenCost").AsNumber(0, 1e12),
                    ExponentialCurve.Read(item.Field("levelCost"))));
            }
            return new SpiritCatalog(all);
        }
    }
}
