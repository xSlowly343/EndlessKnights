using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Net.Nakama
{
    public sealed class OnlineParty : IDisposable
    {
        private readonly NakamaSocket _socket;

        private OnlineParty(NakamaSocket socket, NakamaPartyTransport transport, PartyView party)
        {
            _socket = socket;
            Transport = transport;
            Party = party;
        }

        public NakamaPartyTransport Transport { get; }
        public PartyView Party { get; }
        public bool Connected => _socket.IsOpen;

        public static async Task<OnlineParty?> ConnectAsync(NakamaClient client, NakamaSession session, ulong playerNumber, CancellationToken cancel = default)
        {
            var directory = new PartyDirectory(client, session);
            var party = await directory.MineAsync(cancel).ConfigureAwait(false);
            if (party == null)
                return null;
            var matchId = await directory.ConnectAsync(cancel).ConfigureAwait(false);
            var socket = new NakamaSocket();
            try
            {
                await socket.ConnectAsync(client.ServerUrl, session, cancel).ConfigureAwait(false);
                await socket.JoinMatchAsync(matchId, new Dictionary<string, string> { ["playerNumber"] = playerNumber.ToString(CultureInfo.InvariantCulture) }, cancel)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                socket.Dispose();
                throw;
            }
            return new OnlineParty(socket, new NakamaPartyTransport(socket, matchId, playerNumber), party);
        }

        public void Dispose() => _socket.Dispose();
    }
}
