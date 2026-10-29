using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyFeat
    {
        public PartyFeat(string id, string kind, IReadOnlyList<double> levels, IReadOnlyList<int> banner, string? title, bool tracked)
        {
            Id = id;
            Kind = kind;
            Levels = levels;
            Banner = banner;
            Title = title;
            Tracked = tracked;
        }

        public string Id { get; }
        public string Kind { get; }
        public IReadOnlyList<double> Levels { get; }
        public IReadOnlyList<int> Banner { get; }
        public string? Title { get; }
        public bool Tracked { get; }
    }

    public sealed class PartyChronicleRules
    {
        public const string FileName = "party_chronicle.json";
        public const int SupportedVersion = 1;

        public static readonly IReadOnlyList<string> TrackedKinds = new[] { "bossesTogether", "fullOrchestra", "hoursTogether", "nightBosses", "riftDepth", "mentorHours", "guardiansTogether", "synergiesOnOneEnemy" };
        public static readonly IReadOnlyList<string> LaterKinds = System.Array.Empty<string>();

        private PartyChronicleRules(double flushSeconds, int maxRecords, int nightUtcOffsetMinutes, int nightFromHour, int nightToHour, IReadOnlyList<PartyFeat> feats)
        {
            NightUtcOffsetMinutes = nightUtcOffsetMinutes;
            NightFromHour = nightFromHour;
            NightToHour = nightToHour;
            FlushSeconds = flushSeconds;
            MaxRecords = maxRecords;
            Feats = feats;
        }

        public double FlushSeconds { get; }
        public int MaxRecords { get; }
        public int NightUtcOffsetMinutes { get; }
        public int NightFromHour { get; }
        public int NightToHour { get; }
        public IReadOnlyList<PartyFeat> Feats { get; }

        public static PartyChronicleRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var feats = new List<PartyFeat>();
            var ids = new HashSet<string>();
            foreach (var item in root.Field("feats").Items())
                feats.Add(ReadFeat(item, ids));
            var night = root.Field("night");
            var from = night.Field("fromHour").AsInt(0, 23);
            var to = night.Field("toHour").AsInt(1, 24);
            if (to <= from)
                throw night.Field("toHour").Error($"must be later than fromHour ({from})");
            return new PartyChronicleRules(root.Field("flushSeconds").AsNumber(5, 3600), root.Field("maxRecords").AsInt(1, 500),
                night.Field("utcOffsetMinutes").AsInt(-720, 840), from, to, feats);
        }

        private static PartyFeat ReadFeat(ContentNode node, HashSet<string> ids)
        {
            var id = node.Field("id").AsString();
            if (id.Length == 0 || !ids.Add(id))
                throw node.Field("id").Error("must be a new, non-empty id");
            var kindNode = node.Field("kind");
            var kind = kindNode.AsString();
            var tracked = !node.HasField("tracked") || node.Field("tracked").AsBool();
            if (!Contains(TrackedKinds, kind) && !Contains(LaterKinds, kind))
                throw kindNode.Error($"unknown kind {kind}");
            if (tracked && !Contains(TrackedKinds, kind))
                throw kindNode.Error($"{kind} is not counted yet, mark the feat \"tracked\": false");
            var levels = new List<double>();
            var levelsNode = node.Field("levels");
            foreach (var level in levelsNode.Items())
            {
                var value = level.AsNumber(1, 1e9);
                if (levels.Count > 0 && value <= levels[levels.Count - 1])
                    throw level.Error("levels must grow");
                levels.Add(value);
            }
            if (levels.Count == 0)
                throw levelsNode.Error("must list at least one level");
            var banner = new List<int>();
            if (node.HasField("banner"))
            {
                var bannerNode = node.Field("banner");
                foreach (var level in bannerNode.Items())
                    banner.Add(level.AsInt(1, 100));
                if (banner.Count != levels.Count)
                    throw bannerNode.Error($"must give a banner level for each of the {levels.Count} levels");
            }
            var title = node.HasField("title") ? node.Field("title").AsString() : null;
            return new PartyFeat(id, kind, levels, banner, title, tracked);
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }
            return false;
        }
    }
}
