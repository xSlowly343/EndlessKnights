using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Sim;

namespace Game.Net.Nakama
{
    public sealed class TownBuildReply
    {
        private TownBuildReply(JsonValue json)
        {
            var voted = CosmeticsMineView.Ids(json, "built");
            Built = voted;
            var candidates = new List<TownCandidate>();
            foreach (var item in NakamaJson.Items(json, "candidates"))
                candidates.Add(new TownCandidate(NakamaJson.Text(item, "project"), (long)NakamaJson.Number(item, "votes")));
            var stocks = new List<TownStock>();
            foreach (var item in NakamaJson.Items(json, "resources"))
            {
                stocks.Add(new TownStock(NakamaJson.Text(item, "id"), (long)NakamaJson.Number(item, "stock"), (long)NakamaJson.Number(item, "need"),
                    (long)NakamaJson.Number(item, "yours"), (long)NakamaJson.Number(item, "sentToday"), (long)NakamaJson.Number(item, "dailyCap")));
            }
            var accepted = new Dictionary<string, long>(System.StringComparer.Ordinal);
            var acceptedJson = NakamaJson.Field(json, "accepted");
            if (acceptedJson != null && acceptedJson.Kind == JsonKind.Object)
            {
                foreach (var field in acceptedJson.Fields)
                {
                    if (field.Value.Kind == JsonKind.Number)
                        accepted[field.Key] = (long)field.Value.Number;
                }
            }
            Accepted = accepted;
            var top = new List<TownBuilder>();
            foreach (var item in NakamaJson.Items(json, "top"))
                top.Add(new TownBuilder((int)NakamaJson.Number(item, "rank"), NakamaJson.Text(item, "name"), (long)NakamaJson.Number(item, "points")));
            Board = new TownBoard(TownBoard.PhaseOf(NakamaJson.Text(json, "phase")), (int)NakamaJson.Number(json, "round"), (long)NakamaJson.Number(json, "voteEnds"),
                (long)NakamaJson.Number(json, "opensAt"), (long)NakamaJson.Number(json, "now"), NakamaJson.Text(json, "project"), NakamaJson.Text(json, "vote"),
                voted, candidates, stocks).WithBuilders((long)NakamaJson.Number(json, "points"), (int)NakamaJson.Number(json, "rank"), top).WithRoles(Roles(json));
        }

        private static IReadOnlyList<TownRole> Roles(JsonValue json)
        {
            var roles = new List<TownRole>();
            foreach (var item in NakamaJson.Items(json, "roles"))
                roles.Add(new TownRole(NakamaJson.Text(item, "role"), NakamaJson.Text(item, "name"), (long)NakamaJson.Number(item, "amount")));
            return roles;
        }

        public IReadOnlyList<string> Built { get; }
        public TownBoard Board { get; }
        public IReadOnlyDictionary<string, long> Accepted { get; }

        public static TownBuildReply From(JsonValue json) => new TownBuildReply(json);

        public static string AddPayload(IReadOnlyDictionary<string, long> gains)
        {
            var ids = new List<string>(gains.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var fields = new (string Name, string Json)[ids.Count];
            for (var i = 0; i < fields.Length; i++)
                fields[i] = (ids[i], gains[ids[i]].ToString(CultureInfo.InvariantCulture));
            return NakamaJson.Object(("gains", NakamaJson.Object(fields)));
        }

        public static string VotePayload(string project) => NakamaJson.Object(("project", NakamaJson.Quote(project)));

        public static async Task<TownBuildReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_build", "{}", cancel).ConfigureAwait(false)));

        public static async Task<TownBuildReply> AddAsync(NakamaClient client, NakamaSession session, IReadOnlyDictionary<string, long> gains, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_build_add", AddPayload(gains), cancel).ConfigureAwait(false)));

        public static async Task<TownBuildReply> VoteAsync(NakamaClient client, NakamaSession session, string project, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_build_vote", VotePayload(project), cancel).ConfigureAwait(false)));
    }
}
