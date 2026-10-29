using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ClanRequestsView
    {
        private ClanRequestsView(JsonValue json)
        {
            ClanId = NakamaJson.Text(json, "clanId");
            Days = (int)NakamaJson.Number(json, "days");
            var requests = new List<ClanRequestRow>();
            foreach (var request in NakamaJson.Items(json, "requests"))
                requests.Add(new ClanRequestRow(request));
            Requests = requests;
        }

        public string ClanId { get; }
        public int Days { get; }
        public IReadOnlyList<ClanRequestRow> Requests { get; }

        public static ClanRequestsView From(JsonValue json) => new ClanRequestsView(json);
    }

    public sealed class ClanRequestRow
    {
        public ClanRequestRow(JsonValue json)
        {
            UserId = NakamaJson.Text(json, "userId");
            Name = NakamaJson.Text(json, "name");
            AtUnixMs = (long)NakamaJson.Number(json, "at");
            UntilUnixMs = (long)NakamaJson.Number(json, "until");
        }

        public string UserId { get; }
        public string Name { get; }
        public long AtUnixMs { get; }
        public long UntilUnixMs { get; }
    }

    public sealed class ClanApplicationRow
    {
        public ClanApplicationRow(JsonValue json)
        {
            ClanId = NakamaJson.Text(json, "clanId");
            Adjective = NakamaJson.Text(json, "adjective");
            Noun = NakamaJson.Text(json, "noun");
            Members = (int)NakamaJson.Number(json, "members");
            UntilUnixMs = (long)NakamaJson.Number(json, "until");
        }

        public string ClanId { get; }
        public string Adjective { get; }
        public string Noun { get; }
        public int Members { get; }
        public long UntilUnixMs { get; }
    }

    public sealed class ClanChronicleView
    {
        private ClanChronicleView(JsonValue json)
        {
            ClanId = NakamaJson.Text(json, "clanId");
            var titles = new List<string>();
            foreach (var title in NakamaJson.Items(json, "titles"))
                titles.Add(title.Text);
            Titles = titles;
            var feats = new List<PartyFeatView>();
            foreach (var feat in NakamaJson.Items(json, "feats"))
                feats.Add(new PartyFeatView(feat));
            Feats = feats;
            var records = new List<PartyRecordView>();
            foreach (var record in NakamaJson.Items(json, "records"))
                records.Add(new PartyRecordView(record));
            Records = records;
        }

        public string ClanId { get; }
        public IReadOnlyList<string> Titles { get; }
        public IReadOnlyList<PartyFeatView> Feats { get; }
        public IReadOnlyList<PartyRecordView> Records { get; }

        public static ClanChronicleView From(JsonValue json) => new ClanChronicleView(json);
    }
}
