using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class HideoutPet
    {
        public HideoutPet(string userId, string owner, string companion, string name, int stage, bool mine)
        {
            UserId = userId;
            Owner = owner;
            Companion = companion;
            Name = name;
            Stage = stage;
            Mine = mine;
        }

        public string UserId { get; }
        public string Owner { get; }
        public string Companion { get; }
        public string Name { get; }
        public int Stage { get; }
        public bool Mine { get; }
    }

    public sealed class FarTripMember
    {
        public FarTripMember(string owner, string companion, bool mine)
        {
            Owner = owner;
            Companion = companion;
            Mine = mine;
        }

        public string Owner { get; }
        public string Companion { get; }
        public bool Mine { get; }
    }

    public sealed class CompanionCoopReply
    {
        private CompanionCoopReply(JsonValue? json)
        {
            var pets = new List<HideoutPet>();
            var sent = new List<FarTripMember>();
            var biomes = new List<string>();
            var warmed = new List<string>();
            if (json != null && json.Kind == JsonKind.Object)
            {
                foreach (var entry in NakamaJson.Items(json, "pets"))
                    pets.Add(new HideoutPet(NakamaJson.Text(entry, "userId"), NakamaJson.Text(entry, "owner"), NakamaJson.Text(entry, "companion"), NakamaJson.Text(entry, "name"),
                        (int)NakamaJson.Number(entry, "stage"), NakamaJson.Flag(entry, "mine")));
                PetsLeft = (int)NakamaJson.Number(json, "petsLeft");
                warmed.AddRange(CosmeticsMineView.Ids(json, "warmed"));
                FarLeft = (int)NakamaJson.Number(json, "farLeft");
                var far = NakamaJson.Field(json, "far");
                if (far != null && far.Kind == JsonKind.Object)
                {
                    foreach (var entry in NakamaJson.Items(far, "sent"))
                        sent.Add(new FarTripMember(NakamaJson.Text(entry, "owner"), NakamaJson.Text(entry, "companion"), NakamaJson.Flag(entry, "mine")));
                    biomes.AddRange(CosmeticsMineView.Ids(far, "biomes"));
                    FarEndsAt = (long)NakamaJson.Number(far, "endsAt");
                    FarRunning = NakamaJson.Flag(far, "running");
                    FarReady = NakamaJson.Flag(far, "ready");
                }
            }
            Pets = pets;
            FarSent = sent;
            FarBiomes = biomes;
            Warmed = warmed;
        }

        public IReadOnlyList<HideoutPet> Pets { get; }
        public int PetsLeft { get; }
        public IReadOnlyList<string> Warmed { get; }
        public IReadOnlyList<FarTripMember> FarSent { get; }
        public IReadOnlyList<string> FarBiomes { get; }
        public long FarEndsAt { get; }
        public bool FarRunning { get; }
        public bool FarReady { get; }
        public int FarLeft { get; }

        public static CompanionCoopReply From(JsonValue json) => new CompanionCoopReply(NakamaJson.Field(json, "companions"));

        public static string PlacePayload(string companion, string name, int stage) => companion.Length == 0
            ? NakamaJson.Object(("companion", "null"))
            : NakamaJson.Object(("companion", NakamaJson.Quote(companion)), ("name", NakamaJson.Quote(name)), ("stage", stage.ToString(CultureInfo.InvariantCulture)));

        public static string OwnerPayload(string ownerId) => NakamaJson.Object(("owner", NakamaJson.Quote(ownerId)));

        public static string CompanionPayload(string companion) =>
            NakamaJson.Object(("companion", companion.Length == 0 ? "null" : NakamaJson.Quote(companion)));

        public static async Task<HideoutReply> PlaceAsync(NakamaClient client, NakamaSession session, string companion, string name, int stage, CancellationToken cancel = default) =>
            HideoutReply.From(NakamaJson.Parse(await client.RpcAsync(session, "companion_place", PlacePayload(companion, name, stage), cancel).ConfigureAwait(false)));

        public static async Task<(int Granted, HideoutReply Hideout)> PetAsync(NakamaClient client, NakamaSession session, string ownerId, CancellationToken cancel = default)
        {
            var json = NakamaJson.Parse(await client.RpcAsync(session, "companion_pet", OwnerPayload(ownerId), cancel).ConfigureAwait(false));
            return ((int)NakamaJson.Number(json, "granted"), HideoutReply.From(NakamaJson.Field(json, "hideout") ?? json));
        }

        public static async Task<HideoutReply> WarmAsync(NakamaClient client, NakamaSession session, string ownerId, CancellationToken cancel = default) =>
            HideoutReply.From(NakamaJson.Parse(await client.RpcAsync(session, "companion_warm", OwnerPayload(ownerId), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> FarJoinAsync(NakamaClient client, NakamaSession session, string companion, CancellationToken cancel = default) =>
            HideoutReply.From(NakamaJson.Parse(await client.RpcAsync(session, "companion_far_join", CompanionPayload(companion), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> FarStartAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            HideoutReply.From(NakamaJson.Parse(await client.RpcAsync(session, "companion_far_start", "{}", cancel).ConfigureAwait(false)));

        public static async Task<(IReadOnlyDictionary<string, long> Items, int Granted, HideoutReply Hideout)> FarClaimAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default)
        {
            var json = NakamaJson.Parse(await client.RpcAsync(session, "companion_far_claim", "{}", cancel).ConfigureAwait(false));
            return (HideoutReply.ClaimedItems(json), (int)NakamaJson.Number(json, "granted"), HideoutReply.From(NakamaJson.Field(json, "hideout") ?? json));
        }

        public static async Task<(string Companion, long Friendship)> ShopBuyAsync(NakamaClient client, NakamaSession session, string companion, CancellationToken cancel = default)
        {
            var json = NakamaJson.Parse(await client.RpcAsync(session, "companion_shop_buy", CompanionPayload(companion), cancel).ConfigureAwait(false));
            return (NakamaJson.Text(json, "companion"), (long)NakamaJson.Number(json, "friendship"));
        }
    }
}
