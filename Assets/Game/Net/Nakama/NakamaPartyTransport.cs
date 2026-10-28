using Game.Core.Party.Transport;

namespace Game.Net.Nakama
{
    public sealed class NakamaPartyTransport : IPartyTransport
    {
        public const ulong HostId = 1;

        private readonly NakamaSocket _socket;
        private readonly string _matchId;

        public NakamaPartyTransport(NakamaSocket socket, string matchId, ulong playerNumber)
        {
            _socket = socket;
            _matchId = matchId;
            LocalId = playerNumber;
        }

        public ulong LocalId { get; }
        public string MatchId => _matchId;
        public bool Connected => _socket.IsOpen;

        public void Send(ulong to, byte[] data, int length)
        {
            var buffer = _socket.RentBuffer();
            if (length > buffer.Length)
                return;
            System.Buffer.BlockCopy(data, 0, buffer, 0, length);
            _socket.SendMatchData(_matchId, buffer, length);
        }

        public bool TryReceive(byte[] buffer, out int length, out ulong from)
        {
            from = HostId;
            length = 0;
            while (_socket.TryTakePacket(out var packet))
            {
                if (packet.Length == 0 || packet.Length > buffer.Length)
                    continue;
                System.Buffer.BlockCopy(packet, 0, buffer, 0, packet.Length);
                length = packet.Length;
                return true;
            }
            return false;
        }
    }
}
