using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class RiftBoardView
    {
        private RiftBoardView(JsonValue json)
        {
            RiftId = (long)NakamaJson.Number(json, "riftId");
            EndUnixMs = (long)NakamaJson.Number(json, "endUnixMs");
            Settled = NakamaJson.Flag(json, "settled");
            Parties = (int)NakamaJson.Number(json, "parties");
            var rows = new List<RiftBoardRow>();
            foreach (var row in NakamaJson.Items(json, "top"))
                rows.Add(new RiftBoardRow((int)NakamaJson.Number(row, "place"), NakamaJson.Text(row, "title"), (long)NakamaJson.Number(row, "depth"),
                    (int)NakamaJson.Number(row, "members"), NakamaJson.Flag(row, "yours")));
            Top = rows;
            var own = NakamaJson.Field(json, "own");
            if (own != null && own.Kind == JsonKind.Object)
            {
                OwnPlace = (int)NakamaJson.Number(own, "place");
                OwnDepth = (long)NakamaJson.Number(own, "depth");
            }
        }

        public long RiftId { get; }
        public long EndUnixMs { get; }
        public bool Settled { get; }
        public int Parties { get; }
        public IReadOnlyList<RiftBoardRow> Top { get; }
        public int OwnPlace { get; }
        public long OwnDepth { get; }

        public static RiftBoardView? From(JsonValue? json) => json == null || json.Kind != JsonKind.Object ? null : new RiftBoardView(json);
    }

    public sealed class RiftBoardRow
    {
        public RiftBoardRow(int place, string title, long depth, int members, bool yours)
        {
            Place = place;
            Title = title;
            Depth = depth;
            Members = members;
            Yours = yours;
        }

        public int Place { get; }
        public string Title { get; }
        public long Depth { get; }
        public int Members { get; }
        public bool Yours { get; }
    }
}
