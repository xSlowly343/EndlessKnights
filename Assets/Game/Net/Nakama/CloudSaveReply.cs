using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Save;

namespace Game.Net.Nakama
{
    public sealed class CloudSaveReply
    {
        public const string Saved = "saved";
        public const string Doubtful = "doubtful";
        public const string Older = "older";

        private CloudSaveReply(JsonValue json)
        {
            Status = NakamaJson.Text(json, "status");
            var save = NakamaJson.Field(json, "save");
            Save = save != null && save.Kind == JsonKind.String ? save.Text : string.Empty;
            var server = NakamaJson.Field(json, "server");
            if (server != null && server.Kind == JsonKind.Object)
            {
                Server = new CloudMark(NakamaJson.Number(server, "playSeconds"), (long)NakamaJson.Number(server, "record"), (long)NakamaJson.Number(server, "savedAt"));
                Trusted = NakamaJson.Flag(server, "trusted");
                Reason = NakamaJson.Text(server, "reason");
            }
        }

        public string Status { get; }
        public string Save { get; }
        public CloudMark? Server { get; }
        public bool Trusted { get; }
        public string Reason { get; } = string.Empty;
        public bool HasSave => Save.Length > 0;

        public static CloudSaveReply From(JsonValue json) => new CloudSaveReply(json);

        public static string PutPayload(string save, bool force) =>
            NakamaJson.Object(("save", NakamaJson.Quote(save)), ("force", force ? "true" : "false"));

        public static async Task<CloudSaveReply> PutAsync(NakamaClient client, NakamaSession session, string save, bool force = false, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "cloud_save_put", PutPayload(save, force), cancel).ConfigureAwait(false)));

        public static async Task<CloudSaveReply> GetAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "cloud_save_get", string.Empty, cancel).ConfigureAwait(false)));

        public static async Task<CloudSaveReply> MetaAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "cloud_save_meta", string.Empty, cancel).ConfigureAwait(false)));
    }
}
