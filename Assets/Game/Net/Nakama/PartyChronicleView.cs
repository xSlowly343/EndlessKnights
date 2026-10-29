using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class PartyChronicleView
    {
        private PartyChronicleView(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Banner = (int)NakamaJson.Number(json, "banner");
            var titles = new List<string>();
            foreach (var title in NakamaJson.Items(json, "titles"))
                titles.Add(title.Text);
            Titles = titles;
            var stats = NakamaJson.Field(json, "stats");
            BossesTogether = stats == null ? 0 : (long)NakamaJson.Number(stats, "bossesTogether");
            HoursTogether = stats == null ? 0 : NakamaJson.Number(stats, "hoursTogether");
            RiftDepth = stats == null ? 0 : (long)NakamaJson.Number(stats, "riftDepth");
            MentorHours = stats == null ? 0 : NakamaJson.Number(stats, "mentorHours");
            var feats = new List<PartyFeatView>();
            foreach (var feat in NakamaJson.Items(json, "feats"))
                feats.Add(new PartyFeatView(feat));
            Feats = feats;
            var records = new List<PartyRecordView>();
            foreach (var record in NakamaJson.Items(json, "records"))
                records.Add(new PartyRecordView(record));
            Records = records;
        }

        public string PartyId { get; }
        public int Banner { get; }
        public IReadOnlyList<string> Titles { get; }
        public long BossesTogether { get; }
        public double HoursTogether { get; }
        public long RiftDepth { get; }
        public double MentorHours { get; }
        public IReadOnlyList<PartyFeatView> Feats { get; }
        public IReadOnlyList<PartyRecordView> Records { get; }

        public static PartyChronicleView? From(JsonValue? json) => json == null || json.Kind != JsonKind.Object ? null : new PartyChronicleView(json);
    }

    public sealed class PartyFeatView
    {
        public PartyFeatView(JsonValue json)
        {
            Id = NakamaJson.Text(json, "id");
            Kind = NakamaJson.Text(json, "kind");
            Tracked = NakamaJson.Flag(json, "tracked");
            Level = (int)NakamaJson.Number(json, "level");
            AtUnixMs = (long)NakamaJson.Number(json, "at");
            Progress = NakamaJson.Number(json, "progress");
            var levels = new List<double>();
            foreach (var level in NakamaJson.Items(json, "levels"))
                levels.Add(level.Number);
            Levels = levels;
        }

        public string Id { get; }
        public string Kind { get; }
        public bool Tracked { get; }
        public int Level { get; }
        public long AtUnixMs { get; }
        public double Progress { get; }
        public IReadOnlyList<double> Levels { get; }
    }

    public sealed class PartyRecordView
    {
        public PartyRecordView(JsonValue json)
        {
            AtUnixMs = (long)NakamaJson.Number(json, "at");
            Kind = NakamaJson.Text(json, "kind");
            Name = NakamaJson.Text(json, "name");
            Feat = NakamaJson.Text(json, "feat");
            Level = (int)NakamaJson.Number(json, "level");
        }

        public long AtUnixMs { get; }
        public string Kind { get; }
        public string Name { get; }
        public string Feat { get; }
        public int Level { get; }
    }
}
