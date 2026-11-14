using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct PartyMascotBond
    {
        public PartyMascotBond(string userId, string name, long bond, int hearts)
        {
            UserId = userId;
            Name = name;
            Bond = bond;
            Hearts = hearts;
        }

        public string UserId { get; }
        public string Name { get; }
        public long Bond { get; }
        public int Hearts { get; }
    }

    public readonly struct PartyMascotTrialView
    {
        public PartyMascotTrialView(string id, string stage, int need, int count, bool done)
        {
            Id = id;
            Stage = stage;
            Need = need;
            Count = count;
            Done = done;
        }

        public string Id { get; }
        public string Stage { get; }
        public int Need { get; }
        public int Count { get; }
        public bool Done { get; }
    }

    public sealed class PartyMascotReply
    {
        private PartyMascotReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Leader = NakamaJson.Flag(json, "leader");
            Beast = NakamaJson.Text(json, "beast");
            Experience = (long)NakamaJson.Number(json, "experience");
            Level = (int)NakamaJson.Number(json, "level");
            Into = (long)NakamaJson.Number(json, "into");
            Need = NakamaJson.Number(json, "need");
            Stage = NakamaJson.Text(json, "stage");
            Temper = NakamaJson.Text(json, "temper");
            Branch = NakamaJson.Text(json, "branch");
            BranchOpen = NakamaJson.Flag(json, "branchOpen");
            Coat = NakamaJson.Text(json, "coat");
            Coats = PartyTreasuryReply.Texts(NakamaJson.Items(json, "coats"));
            GearOwned = PartyTreasuryReply.Texts(NakamaJson.Items(json, "gearOwned"));
            ChangeAt = (long)NakamaJson.Number(json, "changeAt");
            FeedLeft = (int)NakamaJson.Number(json, "feedLeft");
            PetsLeft = (int)NakamaJson.Number(json, "petsLeft");
            YourBond = (long)NakamaJson.Number(json, "yourBond");
            YourHearts = (int)NakamaJson.Number(json, "yourHearts");
            Votes = Texts(NakamaJson.Field(json, "votes"));
            Gear = Texts(NakamaJson.Field(json, "gear"));
            var bonds = new List<PartyMascotBond>();
            foreach (var bond in NakamaJson.Items(json, "bonds"))
                bonds.Add(new PartyMascotBond(NakamaJson.Text(bond, "userId"), NakamaJson.Text(bond, "name"), (long)NakamaJson.Number(bond, "bond"), (int)NakamaJson.Number(bond, "hearts")));
            Bonds = bonds;
            Held = NakamaJson.Text(json, "held");
            RitePoints = (int)NakamaJson.Number(json, "ritePoints");
            RiteMaxRank = (int)NakamaJson.Number(json, "riteMaxRank");
            RiteResetAt = (long)NakamaJson.Number(json, "riteResetAt");
            Favourite = NakamaJson.Text(json, "favourite");
            Gauge = NakamaJson.Number(json, "gauge");
            GaugeFull = NakamaJson.Number(json, "gaugeFull");
            CallId = (long)NakamaJson.Number(json, "callId");
            CallPending = NakamaJson.Flag(json, "callPending");
            var rites = new Dictionary<string, int>();
            var riteField = NakamaJson.Field(json, "rites");
            if (riteField != null && riteField.Kind == JsonKind.Object)
            {
                foreach (var pair in riteField.Fields)
                {
                    if (pair.Value.Kind == JsonKind.Number)
                        rites[pair.Key] = (int)pair.Value.Number;
                }
            }
            Rites = rites;
            var trials = new List<PartyMascotTrialView>();
            foreach (var trial in NakamaJson.Items(json, "trials"))
                trials.Add(new PartyMascotTrialView(NakamaJson.Text(trial, "id"), NakamaJson.Text(trial, "stage"), (int)NakamaJson.Number(trial, "need"),
                    (int)NakamaJson.Number(trial, "count"), NakamaJson.Flag(trial, "done")));
            Trials = trials;
            var fed = new Dictionary<string, int>();
            FedItems = fed;
            var accepted = NakamaJson.Field(json, "accepted");
            if (accepted != null && accepted.Kind == JsonKind.Object)
            {
                FedPortions = (int)NakamaJson.Number(accepted, "portions");
                foreach (var pair in HideoutReply.Amounts(NakamaJson.Field(accepted, "items")))
                    fed[pair.Key] = (int)System.Math.Min(int.MaxValue, pair.Value);
                FedExperience = NakamaJson.Number(accepted, "experience");
                FedFavourite = NakamaJson.Flag(accepted, "favourite");
                Call = NakamaJson.Flag(accepted, "call");
                Calls = (int)NakamaJson.Number(accepted, "calls");
            }
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public bool Leader { get; }
        public string Beast { get; }
        public long Experience { get; }
        public int Level { get; }
        public long Into { get; }
        public double Need { get; }
        public string Stage { get; }
        public string Temper { get; }
        public string Branch { get; }
        public bool BranchOpen { get; }
        public string Coat { get; }
        public IReadOnlyList<string> Coats { get; }
        public IReadOnlyList<string> GearOwned { get; }
        public IReadOnlyDictionary<string, string> Gear { get; }
        public IReadOnlyDictionary<string, string> Votes { get; }
        public long ChangeAt { get; }
        public int FeedLeft { get; }
        public int PetsLeft { get; }
        public long YourBond { get; }
        public int YourHearts { get; }
        public IReadOnlyList<PartyMascotBond> Bonds { get; }
        public int FedPortions { get; }
        public IReadOnlyDictionary<string, int> FedItems { get; }
        public double FedExperience { get; }
        public bool FedFavourite { get; }
        public bool Call { get; }
        public int Calls { get; }
        public string Held { get; }
        public int RitePoints { get; }
        public int RiteMaxRank { get; }
        public long RiteResetAt { get; }
        public string Favourite { get; }
        public double Gauge { get; }
        public double GaugeFull { get; }
        public long CallId { get; }
        public bool CallPending { get; }
        public IReadOnlyDictionary<string, int> Rites { get; }
        public IReadOnlyList<PartyMascotTrialView> Trials { get; }
        public double GaugeShare => GaugeFull > 0 ? System.Math.Min(1, Gauge / GaugeFull) : 0;

        public static PartyMascotReply From(JsonValue json) => new PartyMascotReply(json);

        public static string ChoosePayload(string beast) => NakamaJson.Object(("beast", NakamaJson.Quote(beast)));

        public static string FeedPayload(string food, IReadOnlyDictionary<string, int> items)
        {
            var ids = new List<string>(items.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var fields = new (string, string)[ids.Count];
            for (var i = 0; i < ids.Count; i++)
                fields[i] = (ids[i], items[ids[i]].ToString(CultureInfo.InvariantCulture));
            return NakamaJson.Object(("food", NakamaJson.Quote(food)), ("items", NakamaJson.Object(fields)));
        }

        public static string BranchPayload(string branch) => NakamaJson.Object(("branch", NakamaJson.Quote(branch)));

        public static string RitePayload(string id) => NakamaJson.Object(("id", NakamaJson.Quote(id)));

        public static string ActionsPayload(double actions) =>
            NakamaJson.Object(("actions", System.Math.Max(0, System.Math.Round(actions, 2)).ToString("0.##", CultureInfo.InvariantCulture)));

        public static Task<PartyMascotReply> RiteAsync(NakamaClient client, NakamaSession session, string id, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_rite", RitePayload(id), cancel);

        public static Task<PartyMascotReply> RiteResetAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_rite_reset", "{}", cancel);

        public static Task<PartyMascotReply> ActionsAsync(NakamaClient client, NakamaSession session, double actions, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_actions", ActionsPayload(actions), cancel);

        public static Task<PartyMascotReply> CallTakeAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_call_take", "{}", cancel);

        public static string LookPayload(string? coat, IReadOnlyDictionary<string, string>? gear)
        {
            var fields = new List<(string, string)>();
            if (coat != null)
                fields.Add(("coat", NakamaJson.Quote(coat)));
            if (gear != null)
            {
                var slots = new List<(string, string)>();
                foreach (var pair in gear)
                    slots.Add((pair.Key, NakamaJson.Quote(pair.Value)));
                fields.Add(("gear", NakamaJson.Object(slots.ToArray())));
            }
            return NakamaJson.Object(fields.ToArray());
        }

        public static Task<PartyMascotReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot", "{}", cancel);

        public static Task<PartyMascotReply> ChooseAsync(NakamaClient client, NakamaSession session, string beast, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_choose", ChoosePayload(beast), cancel);

        public static Task<PartyMascotReply> FeedAsync(NakamaClient client, NakamaSession session, string food, IReadOnlyDictionary<string, int> items, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_feed", FeedPayload(food, items), cancel);

        public static Task<PartyMascotReply> PetAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_pet", "{}", cancel);

        public static Task<PartyMascotReply> BranchAsync(NakamaClient client, NakamaSession session, string branch, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_branch", BranchPayload(branch), cancel);

        public static Task<PartyMascotReply> LookAsync(NakamaClient client, NakamaSession session, string? coat, IReadOnlyDictionary<string, string>? gear, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_look", LookPayload(coat, gear), cancel);

        private static IReadOnlyDictionary<string, string> Texts(JsonValue? field)
        {
            var texts = new Dictionary<string, string>();
            if (field == null || field.Kind != JsonKind.Object)
                return texts;
            foreach (var pair in field.Fields)
            {
                if (pair.Value.Kind == JsonKind.String)
                    texts[pair.Key] = pair.Value.Text;
            }
            return texts;
        }

        private static async Task<PartyMascotReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
