using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Party;
using Game.Core.Sim;

namespace Game.Net.Nakama
{
    public sealed class GhostDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public GhostDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<double> ShareAsync(HeroClass heroClass, double shareRate, long activeSeconds, CancellationToken cancel = default)
        {
            var payload = NakamaJson.Object(("share", "true"), ("heroClass", Number((int)heroClass)), ("shareRate", shareRate.ToString("R", CultureInfo.InvariantCulture)),
                ("activeSeconds", activeSeconds.ToString(CultureInfo.InvariantCulture)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "ghost_upload", payload, cancel).ConfigureAwait(false));
            return NakamaJson.Number(reply, "activity");
        }

        public Task StopSharingAsync(CancellationToken cancel = default) => _client.RpcAsync(_session, "ghost_upload", NakamaJson.Object(("share", "false")), cancel);

        public async Task<GhostSnapshot?> PickAsync(string? userId = null, CancellationToken cancel = default)
        {
            var payload = userId == null ? "{}" : NakamaJson.Object(("userId", NakamaJson.Quote(userId)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "ghost_pick", payload, cancel).ConfigureAwait(false));
            var ghost = NakamaJson.Field(reply, "ghost");
            return ghost != null && ghost.Kind == JsonKind.Object ? Snapshot(ghost) : (GhostSnapshot?)null;
        }

        public async Task<long> ThankAsync(string ownerId, long bosses, CancellationToken cancel = default)
        {
            var payload = NakamaJson.Object(("userId", NakamaJson.Quote(ownerId)), ("bosses", bosses.ToString(CultureInfo.InvariantCulture)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "ghost_thanks", payload, cancel).ConfigureAwait(false));
            return (long)NakamaJson.Number(reply, "granted");
        }

        public static GhostSnapshot Snapshot(JsonValue ghost)
        {
            var heroClass = (int)NakamaJson.Number(ghost, "heroClass");
            return new GhostSnapshot(NakamaJson.Text(ghost, "userId"), NakamaJson.Text(ghost, "name"),
                heroClass >= 0 && heroClass <= (int)HeroClass.Golemancer ? (HeroClass)heroClass : HeroClass.Runedancer,
                System.Math.Max(0, NakamaJson.Number(ghost, "shareRate")), System.Math.Max(0, System.Math.Min(1, NakamaJson.Number(ghost, "activity"))));
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
