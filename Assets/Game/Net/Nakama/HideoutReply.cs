using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Sim;

namespace Game.Net.Nakama
{
    public sealed class HideoutReply
    {
        private HideoutReply(JsonValue json)
        {
            Tier = (int)NakamaJson.Number(json, "tier");
            Members = (int)NakamaJson.Number(json, "members");
            Bosses = (long)NakamaJson.Number(json, "bosses");
            Orders = (int)NakamaJson.Number(json, "orders");
            SitSeconds = (long)NakamaJson.Number(json, "sitSeconds");
            FireLevel = (int)NakamaJson.Number(json, "fireLevel");
            var here = new List<string>();
            foreach (var entry in NakamaJson.Items(json, "here"))
                here.Add(NakamaJson.Text(entry, "name"));
            Here = here;
            var fishers = new List<string>();
            foreach (var entry in NakamaJson.Items(json, "fishers"))
                fishers.Add(NakamaJson.Text(entry, "name"));
            Fishers = fishers;
            FishingTogether = NakamaJson.Flag(json, "fishingTogether");
            FishTogether = (long)NakamaJson.Number(json, "fishTogether");
            HoursTogether = (long)NakamaJson.Number(json, "hoursTogether");
            Leader = NakamaJson.Flag(json, "leader");
            Buildings = Counts(NakamaJson.Field(json, "buildings"));
            Wants = Counts(NakamaJson.Field(json, "wants"));
            CanQueue = CosmeticsMineView.Ids(json, "canQueue");
            MyWants = CosmeticsMineView.Ids(json, "mine");
            var corners = new List<HideoutCorner>();
            foreach (var entry in NakamaJson.Items(json, "corners"))
                corners.Add(new HideoutCorner(NakamaJson.Text(entry, "name"), NakamaJson.Text(entry, "hero"), NakamaJson.Text(entry, "path"), CosmeticsMineView.Ids(entry, "decor")));
            Corners = corners;
            CornerDecor = (int)NakamaJson.Number(json, "cornerDecor");
            MyCorner = NakamaJson.Flag(json, "myCorner");
            var commons = NakamaJson.Field(json, "commons");
            var beds = new List<SharedBed>();
            var journal = new List<StorageEntry>();
            if (commons != null && commons.Kind == JsonKind.Object)
            {
                Together = NakamaJson.Flag(commons, "together");
                foreach (var entry in NakamaJson.Items(commons, "garden"))
                    beds.Add(new SharedBed(NakamaJson.Text(entry, "plant"), NakamaJson.Number(entry, "share"), NakamaJson.Flag(entry, "ready"), NakamaJson.Flag(entry, "tended")));
                Owed = Amounts(NakamaJson.Field(commons, "owed"));
                var storage = NakamaJson.Field(commons, "storage");
                StorageBuilt = storage != null && storage.Kind == JsonKind.Object;
                Storage = Amounts(storage);
                foreach (var entry in NakamaJson.Items(commons, "journal"))
                    journal.Add(new StorageEntry(NakamaJson.Text(entry, "name"), NakamaJson.Text(entry, "item"), (long)NakamaJson.Number(entry, "amount")));
                TakenToday = (int)NakamaJson.Number(commons, "takenToday");
                TakePerDay = (int)NakamaJson.Number(commons, "takePerDay");
            }
            Beds = beds;
            Journal = journal;
            var trophies = NakamaJson.Field(json, "trophies");
            var feats = new List<TrophyFeat>();
            if (trophies != null && trophies.Kind == JsonKind.Object)
            {
                TrophyBosses = (long)NakamaJson.Number(trophies, "bosses");
                TrophyGuardians = (long)NakamaJson.Number(trophies, "guardians");
                TrophyRiftDepth = (long)NakamaJson.Number(trophies, "riftDepth");
                TrophyHours = (long)NakamaJson.Number(trophies, "hours");
                foreach (var entry in NakamaJson.Items(trophies, "feats"))
                    feats.Add(new TrophyFeat(NakamaJson.Text(entry, "id"), (int)NakamaJson.Number(entry, "level")));
            }
            TrophyFeats = feats;
            Companions = CompanionCoopReply.From(json);
            var crafts = NakamaJson.Field(json, "crafts");
            ForgePartners = crafts != null && crafts.Kind == JsonKind.Object ? CosmeticsMineView.Ids(crafts, "forge") : System.Array.Empty<string>();
            CauldronPartners = crafts != null && crafts.Kind == JsonKind.Object ? CosmeticsMineView.Ids(crafts, "cauldron") : System.Array.Empty<string>();
            var order = NakamaJson.Field(json, "order");
            var needs = new List<PartyOrderNeed>();
            if (order != null && order.Kind == JsonKind.Object)
            {
                OrderId = NakamaJson.Text(order, "id");
                OrderDone = NakamaJson.Flag(order, "done");
                OrderDaysLeft = (int)NakamaJson.Number(order, "daysLeft");
                OrderCanRedraw = NakamaJson.Flag(order, "canRedraw");
                foreach (var entry in NakamaJson.Items(order, "needs"))
                    needs.Add(new PartyOrderNeed(NakamaJson.Text(entry, "counter"), (long)NakamaJson.Number(entry, "have"), (long)NakamaJson.Number(entry, "target")));
            }
            OrderNeeds = needs;
            var project = NakamaJson.Field(json, "project");
            if (project != null && project.Kind == JsonKind.Object)
            {
                Project = NakamaJson.Text(project, "building");
                ProjectLevel = (int)NakamaJson.Number(project, "level");
                ProjectPoints = (long)NakamaJson.Number(project, "points");
                ProjectTarget = (long)NakamaJson.Number(project, "target");
                ProjectHours = NakamaJson.Number(project, "hours");
                ProjectYours = (long)NakamaJson.Number(project, "yours");
            }
        }

        public string OrderId { get; } = string.Empty;
        public bool OrderDone { get; }
        public int OrderDaysLeft { get; }
        public bool OrderCanRedraw { get; }
        public IReadOnlyList<PartyOrderNeed> OrderNeeds { get; }
        public bool Together { get; }
        public long TrophyBosses { get; }
        public long TrophyGuardians { get; }
        public long TrophyRiftDepth { get; }
        public long TrophyHours { get; }
        public IReadOnlyList<TrophyFeat> TrophyFeats { get; }
        public IReadOnlyList<string> ForgePartners { get; }
        public IReadOnlyList<string> CauldronPartners { get; }
        public IReadOnlyList<SharedBed> Beds { get; }
        public IReadOnlyDictionary<string, long> Owed { get; } = new Dictionary<string, long>();
        public bool StorageBuilt { get; }
        public IReadOnlyDictionary<string, long> Storage { get; } = new Dictionary<string, long>();
        public IReadOnlyList<StorageEntry> Journal { get; }
        public int TakenToday { get; }
        public int TakePerDay { get; }
        public IReadOnlyList<HideoutCorner> Corners { get; }
        public int CornerDecor { get; }
        public bool MyCorner { get; }
        public long HoursTogether { get; }
        public bool Leader { get; }
        public IReadOnlyDictionary<string, int> Buildings { get; }
        public IReadOnlyDictionary<string, int> Wants { get; }
        public IReadOnlyList<string> CanQueue { get; }
        public IReadOnlyList<string> MyWants { get; }
        public string Project { get; } = string.Empty;
        public int ProjectLevel { get; }
        public long ProjectPoints { get; }
        public long ProjectTarget { get; }
        public double ProjectHours { get; }
        public long ProjectYours { get; }

        public static string QueuePayload(string building) => NakamaJson.Object(("building", NakamaJson.Quote(building)));

        public static string GivePayload(string item, int amount) => ClanCampReply.GivePayload(item, amount);

        public static string CornerPayload(string hero, string path, IReadOnlyList<string> decor)
        {
            var items = new string[decor.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = NakamaJson.Quote(decor[i]);
            return NakamaJson.Object(("hero", NakamaJson.Quote(hero)), ("path", NakamaJson.Quote(path)), ("decor", "[" + string.Join(",", items) + "]"));
        }

        public static async Task<HideoutReply> CornerAsync(NakamaClient client, NakamaSession session, string hero, string path, IReadOnlyList<string> decor, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_corner", CornerPayload(hero, path, decor), cancel).ConfigureAwait(false)));

        public static string WantPayload(string building, bool on) => NakamaJson.Object(("building", NakamaJson.Quote(building)), ("on", on ? "true" : "false"));

        public static async Task<HideoutReply> QueueAsync(NakamaClient client, NakamaSession session, string building, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_queue", QueuePayload(building), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> GiveAsync(NakamaClient client, NakamaSession session, string item, int amount, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_give", GivePayload(item, amount), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> OrderRedrawAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_order_redraw", "{}", cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> WantAsync(NakamaClient client, NakamaSession session, string building, bool on, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_want", WantPayload(building, on), cancel).ConfigureAwait(false)));

        public static IReadOnlyDictionary<string, long> Amounts(JsonValue? json)
        {
            var amounts = new Dictionary<string, long>();
            if (json != null && json.Kind == JsonKind.Object)
            {
                foreach (var pair in json.Fields)
                {
                    if (pair.Value.Kind == JsonKind.Number && pair.Value.Number > 0)
                        amounts[pair.Key] = (long)pair.Value.Number;
                }
            }
            return amounts;
        }

        public static string BedPayload(int bed) => NakamaJson.Object(("bed", bed.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public static string PlantPayload(int bed, string plant) =>
            NakamaJson.Object(("bed", bed.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("plant", NakamaJson.Quote(plant)));

        public static string TendPayload(bool here, bool fishing, int caught, double keys, double clicks, double mouseMeters, double idleSeconds, string station = "") =>
            NakamaJson.Object(("here", here ? "true" : "false"), ("fishing", fishing ? "true" : "false"), ("caught", caught.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("tend", NakamaJson.Object(("keys", Whole(keys)), ("clicks", Whole(clicks)), ("mouseMeters", Whole(mouseMeters)), ("idleSeconds", Whole(idleSeconds)))),
                ("craft", station.Length > 0 ? NakamaJson.Quote(station) : "null"));

        public static string BrewedPayload(string recipe, bool fine) => NakamaJson.Object(("recipe", NakamaJson.Quote(recipe)), ("fine", fine ? "true" : "false"));

        public static async Task<HideoutReply> BrewedAsync(NakamaClient client, NakamaSession session, string recipe, bool fine, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_brewed", BrewedPayload(recipe, fine), cancel).ConfigureAwait(false)));

        private static string Whole(double value) => System.Math.Max(0, System.Math.Floor(value)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        public static async Task<HideoutReply> TendAsync(NakamaClient client, NakamaSession session, bool here, bool fishing, int caught, double keys, double clicks, double mouseMeters, double idleSeconds, string station = "", CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_presence", TendPayload(here, fishing, caught, keys, clicks, mouseMeters, idleSeconds, station), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> PlantAsync(NakamaClient client, NakamaSession session, int bed, string plant, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_plant", PlantPayload(bed, plant), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> HarvestAsync(NakamaClient client, NakamaSession session, int bed, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_harvest", BedPayload(bed), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> StorePutAsync(NakamaClient client, NakamaSession session, string item, int amount, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_store_put", GivePayload(item, amount), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> StoreTakeAsync(NakamaClient client, NakamaSession session, string item, int amount, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_store_take", GivePayload(item, amount), cancel).ConfigureAwait(false)));

        public static IReadOnlyDictionary<string, long> ClaimedItems(JsonValue json) => Amounts(NakamaJson.Field(json, "items"));

        public static string ClaimPayload(long ack) => NakamaJson.Object(("ack", ack.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public static long ClaimSeq(JsonValue json) => (long)NakamaJson.Number(json, "seq");

        public static async Task<(long Seq, IReadOnlyDictionary<string, long> Items, HideoutReply Hideout)> ClaimAsync(NakamaClient client, NakamaSession session, long ack, CancellationToken cancel = default)
        {
            var json = NakamaJson.Parse(await client.RpcAsync(session, "hideout_claim", ClaimPayload(ack), cancel).ConfigureAwait(false));
            var hideout = NakamaJson.Field(json, "hideout");
            return (ClaimSeq(json), ClaimedItems(json), From(hideout ?? json));
        }

        private static IReadOnlyDictionary<string, int> Counts(JsonValue? json)
        {
            var counts = new Dictionary<string, int>();
            if (json != null && json.Kind == JsonKind.Object)
            {
                foreach (var pair in json.Fields)
                    counts[pair.Key] = pair.Value.Kind == JsonKind.Number ? (int)pair.Value.Number : 0;
            }
            return counts;
        }

        public CompanionCoopReply Companions { get; }
        public int Tier { get; }
        public int Members { get; }
        public long Bosses { get; }
        public int Orders { get; }
        public long SitSeconds { get; }
        public int FireLevel { get; }
        public IReadOnlyList<string> Here { get; }
        public IReadOnlyList<string> Fishers { get; }
        public bool FishingTogether { get; }
        public long FishTogether { get; }

        public static HideoutReply From(JsonValue json) => new HideoutReply(json);

        public static string PresencePayload(bool here) => NakamaJson.Object(("here", here ? "true" : "false"));

        public static async Task<HideoutReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_view", "{}", cancel).ConfigureAwait(false)));

        public static string PresencePayload(bool here, bool fishing, int caught) =>
            NakamaJson.Object(("here", here ? "true" : "false"), ("fishing", fishing ? "true" : "false"), ("caught", caught.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public static async Task<HideoutReply> PresenceAsync(NakamaClient client, NakamaSession session, bool here, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_presence", PresencePayload(here), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> PresenceAsync(NakamaClient client, NakamaSession session, bool here, bool fishing, int caught, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_presence", PresencePayload(here, fishing, caught), cancel).ConfigureAwait(false)));
    }
}
