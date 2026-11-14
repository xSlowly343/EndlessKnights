using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class PartyAllyView
    {
        internal PartyAllyView(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Name = NakamaJson.Text(json, "name");
            Level = (int)NakamaJson.Number(json, "level");
            Members = (int)NakamaJson.Number(json, "members");
        }

        public string PartyId { get; }
        public string Name { get; }
        public int Level { get; }
        public int Members { get; }

        internal static PartyAllyView? Of(JsonValue? json) => json != null && json.Kind == JsonKind.Object ? new PartyAllyView(json) : null;
    }

    public sealed class PartyAllianceReply
    {
        private PartyAllianceReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Leader = NakamaJson.Flag(json, "leader");
            Ally = PartyAllyView.Of(NakamaJson.Field(json, "ally"));
            Outgoing = PartyAllyView.Of(NakamaJson.Field(json, "outgoing"));
            Since = (long)NakamaJson.Number(json, "since");
            HelpReady = NakamaJson.Flag(json, "helpReady");
            HelpPoints = (long)NakamaJson.Number(json, "helpPoints");
            var incoming = new List<PartyAllyView>();
            foreach (var item in NakamaJson.Items(json, "incoming"))
            {
                var ally = PartyAllyView.Of(item);
                if (ally != null)
                    incoming.Add(ally);
            }
            Incoming = incoming;
            var result = NakamaJson.Field(json, "result");
            if (result != null && result.Kind == JsonKind.Object)
            {
                SentPoints = (long)NakamaJson.Number(result, "points");
                Signs = (long)NakamaJson.Number(result, "signs");
            }
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public bool Leader { get; }
        public PartyAllyView? Ally { get; }
        public PartyAllyView? Outgoing { get; }
        public IReadOnlyList<PartyAllyView> Incoming { get; }
        public long Since { get; }
        public bool HelpReady { get; }
        public long HelpPoints { get; }
        public long SentPoints { get; }
        public long Signs { get; }

        public static PartyAllianceReply From(JsonValue json) => new PartyAllianceReply(json);

        public static string ProposePayload(string code) => NakamaJson.Object(("code", NakamaJson.Quote(code)));

        public static string AnswerPayload(string partyId, bool accept) => NakamaJson.Object(("partyId", NakamaJson.Quote(partyId)), ("accept", accept ? "true" : "false"));

        public static Task<PartyAllianceReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_alliance", "{}", cancel);

        public static Task<PartyAllianceReply> ProposeAsync(NakamaClient client, NakamaSession session, string code, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_alliance_propose", ProposePayload(code), cancel);

        public static Task<PartyAllianceReply> AnswerAsync(NakamaClient client, NakamaSession session, string partyId, bool accept, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_alliance_answer", AnswerPayload(partyId, accept), cancel);

        public static Task<PartyAllianceReply> BreakAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_alliance_break", "{}", cancel);

        public static Task<PartyAllianceReply> HelpAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_alliance_help", "{}", cancel);

        private static async Task<PartyAllianceReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
