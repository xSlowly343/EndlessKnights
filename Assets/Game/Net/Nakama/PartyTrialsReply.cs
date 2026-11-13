using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct PartyTrialStanding
    {
        public PartyTrialStanding(string id, bool open, int best, int triesLeft)
        {
            Id = id;
            Open = open;
            Best = best;
            TriesLeft = triesLeft;
        }

        public string Id { get; }
        public bool Open { get; }
        public int Best { get; }
        public int TriesLeft { get; }
    }

    public sealed class PartyTrialRunView
    {
        internal PartyTrialRunView(JsonValue json)
        {
            Trial = NakamaJson.Text(json, "trial");
            Tier = (int)NakamaJson.Number(json, "tier");
            By = NakamaJson.Text(json, "by");
            Fighters = PartyTreasuryReply.Texts(NakamaJson.Items(json, "fighters"));
            At = (long)NakamaJson.Number(json, "at");
            Until = (long)NakamaJson.Number(json, "until");
        }

        public string Trial { get; }
        public int Tier { get; }
        public string By { get; }
        public IReadOnlyList<string> Fighters { get; }
        public long At { get; }
        public long Until { get; }
    }

    public sealed class PartyTrialResult
    {
        internal PartyTrialResult(JsonValue json)
        {
            Trial = NakamaJson.Text(json, "trial");
            Tier = (int)NakamaJson.Number(json, "tier");
            Best = (int)NakamaJson.Number(json, "best");
            FirstClear = NakamaJson.Flag(json, "firstClear");
            Signs = (long)NakamaJson.Number(json, "signs");
            Experience = NakamaJson.Number(json, "experience");
            Carried = PartyTreasuryReply.Texts(NakamaJson.Items(json, "carried"));
        }

        public string Trial { get; }
        public int Tier { get; }
        public int Best { get; }
        public bool FirstClear { get; }
        public long Signs { get; }
        public double Experience { get; }
        public IReadOnlyList<string> Carried { get; }
    }

    public sealed class PartyTrialsReply
    {
        private PartyTrialsReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Ceiling = (int)NakamaJson.Number(json, "ceiling");
            Carry = NakamaJson.Flag(json, "carry");
            WeekEndsAt = (long)NakamaJson.Number(json, "weekEndsAt");
            var trials = new List<PartyTrialStanding>();
            var best = new List<int>();
            foreach (var trial in NakamaJson.Items(json, "trials"))
            {
                var standing = new PartyTrialStanding(NakamaJson.Text(trial, "id"), NakamaJson.Flag(trial, "open"),
                    (int)NakamaJson.Number(trial, "best"), (int)NakamaJson.Number(trial, "triesLeft"));
                trials.Add(standing);
                best.Add(standing.Best);
            }
            Trials = trials;
            Best = best;
            var run = NakamaJson.Field(json, "run");
            Run = run != null && run.Kind == JsonKind.Object ? new PartyTrialRunView(run) : null;
            var result = NakamaJson.Field(json, "result");
            Result = result != null && result.Kind == JsonKind.Object ? new PartyTrialResult(result) : null;
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public int Ceiling { get; }
        public bool Carry { get; }
        public long WeekEndsAt { get; }
        public IReadOnlyList<PartyTrialStanding> Trials { get; }
        public IReadOnlyList<int> Best { get; }
        public PartyTrialRunView? Run { get; }
        public PartyTrialResult? Result { get; }

        public static PartyTrialsReply From(JsonValue json) => new PartyTrialsReply(json);

        public static string StartPayload(string trial, int tier, IReadOnlyList<string> fighters)
        {
            var quoted = new string[fighters.Count];
            for (var i = 0; i < fighters.Count; i++)
                quoted[i] = NakamaJson.Quote(fighters[i]);
            return NakamaJson.Object(("trial", NakamaJson.Quote(trial)), ("tier", tier.ToString(CultureInfo.InvariantCulture)),
                ("fighters", "[" + string.Join(",", quoted) + "]"));
        }

        public static string FinishPayload(bool cleared) => NakamaJson.Object(("cleared", cleared ? "true" : "false"));

        public static string MarathonPayload(int waves) => NakamaJson.Object(("waves", waves.ToString(CultureInfo.InvariantCulture)));

        public static Task<PartyTrialsReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_trials", "{}", cancel);

        public static Task<PartyTrialsReply> StartAsync(NakamaClient client, NakamaSession session, string trial, int tier, IReadOnlyList<string> fighters, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_trial_start", StartPayload(trial, tier, fighters), cancel);

        public static Task<PartyTrialsReply> FinishAsync(NakamaClient client, NakamaSession session, bool cleared, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_trial_finish", FinishPayload(cleared), cancel);

        public static Task<PartyTrialsReply> FinishMarathonAsync(NakamaClient client, NakamaSession session, int waves, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_trial_finish", MarathonPayload(waves), cancel);

        public static Task<PartyTrialsReply> AbandonAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_trial_abandon", "{}", cancel);

        private static async Task<PartyTrialsReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
