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
            var accepted = NakamaJson.Field(json, "accepted");
            if (accepted != null && accepted.Kind == JsonKind.Object)
            {
                FedPortions = (int)NakamaJson.Number(accepted, "portions");
                FedExperience = NakamaJson.Number(accepted, "experience");
                FedFavourite = NakamaJson.Flag(accepted, "favourite");
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
        public double FedExperience { get; }
        public bool FedFavourite { get; }

        public static PartyMascotReply From(JsonValue json) => new PartyMascotReply(json);

        public static string ChoosePayload(string beast) => NakamaJson.Object(("beast", NakamaJson.Quote(beast)));

        public static string FeedPayload(string food, int amount) =>
            NakamaJson.Object(("food", NakamaJson.Quote(food)), ("amount", amount.ToString(CultureInfo.InvariantCulture)));

        public static string BranchPayload(string branch) => NakamaJson.Object(("branch", NakamaJson.Quote(branch)));

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

        public static Task<PartyMascotReply> FeedAsync(NakamaClient client, NakamaSession session, string food, int amount, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_mascot_feed", FeedPayload(food, amount), cancel);

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
