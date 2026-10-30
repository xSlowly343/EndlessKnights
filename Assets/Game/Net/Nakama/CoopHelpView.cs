using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class CoopHelpView
    {
        private CoopHelpView(JsonValue json)
        {
            Points = (int)NakamaJson.Number(json, "points");
            Max = (int)NakamaJson.Number(json, "max");
            var sources = new List<CoopHelpSource>();
            foreach (var item in NakamaJson.Items(json, "sources"))
                sources.Add(new CoopHelpSource(NakamaJson.Text(item, "source"), (int)NakamaJson.Number(item, "points"), (int)NakamaJson.Number(item, "max"),
                    (int)NakamaJson.Number(item, "progress"), (int)NakamaJson.Number(item, "next")));
            Sources = sources;
        }

        public int Points { get; }
        public int Max { get; }
        public IReadOnlyList<CoopHelpSource> Sources { get; }

        public static CoopHelpView From(JsonValue json) => new CoopHelpView(json);
    }

    public readonly struct CoopHelpSource
    {
        public CoopHelpSource(string source, int points, int max, int progress, int next)
        {
            Source = source;
            Points = points;
            Max = max;
            Progress = progress;
            Next = next;
        }

        public string Source { get; }
        public int Points { get; }
        public int Max { get; }
        public int Progress { get; }
        public int Next { get; }
    }
}
