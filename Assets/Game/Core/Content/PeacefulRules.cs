using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum PeacefulMode
    {
        Battle,
        Camp
    }

    public enum PeacefulPlace
    {
        Refuge,
        Hideout,
        Town,
        ClanCamp
    }

    public sealed class PeacefulActivity
    {
        public PeacefulActivity(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class PeacefulPreset
    {
        public PeacefulPreset(string id, string nameRu, string nameEn, int dayMask, int fromMinute, int toMinute, PeacefulMode mode)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            DayMask = dayMask;
            FromMinute = fromMinute;
            ToMinute = toMinute;
            Mode = mode;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int DayMask { get; }
        public int FromMinute { get; }
        public int ToMinute { get; }
        public PeacefulMode Mode { get; }

        public bool Covers(int dayOfWeek, int minuteOfDay) =>
            (DayMask & (1 << dayOfWeek)) != 0 && minuteOfDay >= FromMinute && minuteOfDay < ToMinute;
    }

    public sealed class PeacefulRules
    {
        public const string FileName = "peaceful.json";
        public const int SupportedVersion = 1;

        public IReadOnlyList<PeacefulActivity> Activities { get; private set; } = Array.Empty<PeacefulActivity>();
        public IReadOnlyList<PeacefulPreset> Presets { get; private set; } = Array.Empty<PeacefulPreset>();
        public double OfflineGoldShare { get; private set; }
        public double TeaBattleMinutes { get; private set; }
        public double TeaIdleMinutes { get; private set; }

        public int IndexOf(string activity)
        {
            for (var i = 0; i < Activities.Count; i++)
            {
                if (Activities[i].Id == activity)
                    return i;
            }
            return -1;
        }

        public PeacefulPreset? PresetById(string id)
        {
            foreach (var preset in Presets)
            {
                if (preset.Id == id)
                    return preset;
            }
            return null;
        }

        public static PeacefulRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var activities = new List<PeacefulActivity>();
            foreach (var node in root.Field("activities").Items())
            {
                var name = node.Field("name");
                activities.Add(new PeacefulActivity(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            if (activities.Count == 0)
                throw root.Field("activities").Error("at least one activity is required");
            var presets = new List<PeacefulPreset>();
            foreach (var node in root.Field("presets").Items())
            {
                var mask = 0;
                foreach (var day in node.Field("days").Items())
                    mask |= 1 << day.AsInt(0, 6);
                var from = node.Field("fromMinute").AsInt(0, 1440);
                var to = node.Field("toMinute").AsInt(0, 1440);
                if (to <= from)
                    throw node.Field("toMinute").Error("must be after fromMinute");
                var modeName = node.Field("mode").AsString();
                var mode = modeName == "camp" ? PeacefulMode.Camp : modeName == "battle" ? PeacefulMode.Battle
                    : throw node.Field("mode").Error($"unknown mode '{modeName}', expected camp or battle");
                var name = node.Field("name");
                presets.Add(new PeacefulPreset(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), mask, from, to, mode));
            }
            var tea = root.Field("teaPause");
            return new PeacefulRules
            {
                Activities = activities,
                Presets = presets,
                OfflineGoldShare = root.Field("offlineGoldShare").AsNumber(0, 1),
                TeaBattleMinutes = tea.Field("battleMinutes").AsNumber(1, 1440),
                TeaIdleMinutes = tea.Field("idleMinutes").AsNumber(0.5, 1440)
            };
        }
    }
}
