using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ClanWarView
    {
        public static readonly IReadOnlyList<string> FrontIds = new[] { "keys", "mouse", "clicks", "bosses", "skills" };

        private ClanWarView(JsonValue json)
        {
            Week = (long)NakamaJson.Number(json, "week");
            Phase = NakamaJson.Text(json, "phase");
            WarStartsAtUnixMs = (long)NakamaJson.Number(json, "warStartsAt");
            WarEndsAtUnixMs = (long)NakamaJson.Number(json, "warEndsAt");
            Joined = NakamaJson.Flag(json, "joined");
            Active = (int)NakamaJson.Number(json, "active");
            MinActive = (int)NakamaJson.Number(json, "minActive");
            Morale = NakamaJson.Number(json, "morale");
            Rating = (int)NakamaJson.Number(json, "rating");
            League = NakamaJson.Text(json, "league");
            Wins = (int)NakamaJson.Number(json, "wins");
            var opponent = NakamaJson.Field(json, "opponent");
            if (opponent != null && opponent.Kind == JsonKind.Object)
                Opponent = new ClanWarOpponent(NakamaJson.Text(opponent, "clanId"), NakamaJson.Text(opponent, "name"), NakamaJson.Flag(opponent, "shadow"),
                    NakamaJson.Flag(opponent, "bye"), (int)NakamaJson.Number(opponent, "active"), NakamaJson.Number(opponent, "morale"));
            var fronts = new List<ClanWarFront>();
            var frontsJson = NakamaJson.Field(json, "fronts");
            if (frontsJson != null && frontsJson.Kind == JsonKind.Object)
            {
                var ours = NakamaJson.Field(frontsJson, "ours");
                var theirs = NakamaJson.Field(frontsJson, "theirs");
                var mine = NakamaJson.Field(frontsJson, "mine");
                Cap = NakamaJson.Number(frontsJson, "cap");
                foreach (var id in FrontIds)
                    fronts.Add(new ClanWarFront(id, ours == null ? 0 : NakamaJson.Number(ours, id), theirs == null ? 0 : NakamaJson.Number(theirs, id), mine == null ? 0 : NakamaJson.Number(mine, id)));
            }
            Fronts = fronts;
            var last = NakamaJson.Field(json, "last");
            if (last != null && last.Kind == JsonKind.Object)
                Last = new ClanWarResultView((long)NakamaJson.Number(last, "week"), NakamaJson.Text(last, "outcome"), (int)NakamaJson.Number(last, "points"),
                    (int)NakamaJson.Number(last, "theirPoints"), (long)NakamaJson.Number(last, "seals"), (int)NakamaJson.Number(last, "rating"), NakamaJson.Text(last, "league"));
        }

        public long Week { get; }
        public string Phase { get; }
        public long WarStartsAtUnixMs { get; }
        public long WarEndsAtUnixMs { get; }
        public bool Joined { get; }
        public int Active { get; }
        public int MinActive { get; }
        public double Morale { get; }
        public int Rating { get; }
        public string League { get; }
        public int Wins { get; }
        public ClanWarOpponent? Opponent { get; }
        public IReadOnlyList<ClanWarFront> Fronts { get; }
        public double Cap { get; }
        public ClanWarResultView? Last { get; }

        public static ClanWarView From(JsonValue json) => new ClanWarView(json);

        public static string JoinPayload(bool join) => NakamaJson.Object(("join", join ? "true" : "false"));

        public static async Task<ClanWarView> ShowAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_war_view", "{}", cancel).ConfigureAwait(false)));

        public static async Task<ClanWarView> JoinAsync(NakamaClient client, NakamaSession session, bool join, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_war_join", JoinPayload(join), cancel).ConfigureAwait(false)));
    }

    public sealed class ClanWarOpponent
    {
        public ClanWarOpponent(string clanId, string name, bool shadow, bool bye, int active, double morale)
        {
            ClanId = clanId;
            Name = name;
            Shadow = shadow;
            Bye = bye;
            Active = active;
            Morale = morale;
        }

        public string ClanId { get; }
        public string Name { get; }
        public bool Shadow { get; }
        public bool Bye { get; }
        public int Active { get; }
        public double Morale { get; }
    }

    public readonly struct ClanWarFront
    {
        public ClanWarFront(string id, double ours, double theirs, double mine)
        {
            Id = id;
            Ours = ours;
            Theirs = theirs;
            Mine = mine;
        }

        public string Id { get; }
        public double Ours { get; }
        public double Theirs { get; }
        public double Mine { get; }
        public bool Leading => Ours > Theirs;
    }

    public sealed class ClanWarResultView
    {
        public ClanWarResultView(long week, string outcome, int points, int theirPoints, long seals, int rating, string league)
        {
            Week = week;
            Outcome = outcome;
            Points = points;
            TheirPoints = theirPoints;
            Seals = seals;
            Rating = rating;
            League = league;
        }

        public long Week { get; }
        public string Outcome { get; }
        public int Points { get; }
        public int TheirPoints { get; }
        public long Seals { get; }
        public int Rating { get; }
        public string League { get; }
    }
}
