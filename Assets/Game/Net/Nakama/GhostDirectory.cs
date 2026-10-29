using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Content.Synergy;
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

        public async Task<double> ShareAsync(HeroClass heroClass, double shareRate, IReadOnlyList<string> marks, long activeSeconds, CancellationToken cancel = default)
        {
            var quoted = new List<string>(marks.Count);
            foreach (var mark in marks)
                quoted.Add(NakamaJson.Quote(mark));
            var payload = NakamaJson.Object(("share", "true"), ("heroClass", Number((int)heroClass)), ("shareRate", shareRate.ToString("R", CultureInfo.InvariantCulture)),
                ("marks", "[" + string.Join(",", quoted) + "]"), ("activeSeconds", activeSeconds.ToString(CultureInfo.InvariantCulture)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "ghost_upload", payload, cancel).ConfigureAwait(false));
            return NakamaJson.Number(reply, "activity");
        }

        public Task StopSharingAsync(CancellationToken cancel = default) => _client.RpcAsync(_session, "ghost_upload", NakamaJson.Object(("share", "false")), cancel);

        public async Task<GhostSnapshot?> PickAsync(SynergyRules synergy, string? userId = null, CancellationToken cancel = default)
        {
            var payload = userId == null ? "{}" : NakamaJson.Object(("userId", NakamaJson.Quote(userId)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "ghost_pick", payload, cancel).ConfigureAwait(false));
            var ghost = NakamaJson.Field(reply, "ghost");
            return ghost != null && ghost.Kind == JsonKind.Object ? Snapshot(ghost, synergy) : (GhostSnapshot?)null;
        }

        public async Task<long> ThankAsync(string ownerId, long bosses, CancellationToken cancel = default)
        {
            var payload = NakamaJson.Object(("userId", NakamaJson.Quote(ownerId)), ("bosses", bosses.ToString(CultureInfo.InvariantCulture)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "ghost_thanks", payload, cancel).ConfigureAwait(false));
            return (long)NakamaJson.Number(reply, "granted");
        }

        public static GhostSnapshot Snapshot(JsonValue ghost, SynergyRules synergy)
        {
            var marks = new List<string>();
            foreach (var mark in NakamaJson.Items(ghost, "marks"))
                marks.Add(mark.Text);
            var heroClass = (int)NakamaJson.Number(ghost, "heroClass");
            return new GhostSnapshot(NakamaJson.Text(ghost, "userId"), NakamaJson.Text(ghost, "name"),
                heroClass >= 0 && heroClass <= (int)HeroClass.Captain ? (HeroClass)heroClass : HeroClass.Viking,
                System.Math.Max(0, NakamaJson.Number(ghost, "shareRate")), synergy.MaskOf(marks), System.Math.Max(0, System.Math.Min(1, NakamaJson.Number(ghost, "activity"))));
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
