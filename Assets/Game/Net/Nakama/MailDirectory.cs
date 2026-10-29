using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Content.Rift;
using Game.Core.Mail;

namespace Game.Net.Nakama
{
    public sealed class MailDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public MailDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<IReadOnlyList<MailLetter>> ListAsync(CancellationToken cancel = default)
        {
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "mail_list", "{}", cancel).ConfigureAwait(false));
            var letters = new List<MailLetter>();
            foreach (var letter in NakamaJson.Items(reply, "letters"))
                letters.Add(Letter(letter));
            return letters;
        }

        public async Task<MailLetter> ClaimAsync(string id, CancellationToken cancel = default)
        {
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "mail_claim", NakamaJson.Object(("id", NakamaJson.Quote(id))), cancel).ConfigureAwait(false));
            var letter = NakamaJson.Field(reply, "letter");
            return letter != null && letter.Kind == JsonKind.Object ? Letter(letter) : throw new NakamaException(0, "the server sent no letter");
        }

        public static MailLetter Letter(JsonValue json)
        {
            var reward = NakamaJson.Field(json, "reward");
            return new MailLetter(NakamaJson.Text(json, "id"), NakamaJson.Text(json, "kind"), (long)NakamaJson.Number(json, "at"), (int)NakamaJson.Number(json, "place"),
                (int)NakamaJson.Number(json, "of"), (long)NakamaJson.Number(json, "depth"), NakamaJson.Text(json, "title"),
                reward == null ? default : new RiftReward((long)NakamaJson.Number(reward, "shards"), (int)NakamaJson.Number(reward, "uncutGems"), (long)NakamaJson.Number(reward, "ingots"),
                    (long)NakamaJson.Number(reward, "friendship")),
                NakamaJson.Flag(json, "claimed"), NakamaJson.Text(json, "gem"));
        }
    }
}
