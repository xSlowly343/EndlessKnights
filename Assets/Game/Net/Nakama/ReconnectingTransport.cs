using Game.Core.Party.Transport;

namespace Game.Net.Nakama
{
    public sealed class ReconnectingTransport : IPartyTransport
    {
        private volatile IPartyTransport? _inner;

        public ReconnectingTransport(ulong localId)
        {
            LocalId = localId;
        }

        public ulong LocalId { get; }
        public bool HasLink => _inner != null;

        public void Use(IPartyTransport? inner) => _inner = inner;

        public void Send(ulong to, byte[] data, int length) => _inner?.Send(to, data, length);

        public bool TryReceive(byte[] buffer, out int length, out ulong from)
        {
            var inner = _inner;
            if (inner != null)
                return inner.TryReceive(buffer, out length, out from);
            length = 0;
            from = 0;
            return false;
        }
    }
}
