namespace Game.Core.Party.Transport
{
    public sealed class LoopbackTransport : IPartyTransport
    {
        private readonly LoopbackNetwork _network;

        internal LoopbackTransport(LoopbackNetwork network, ulong id)
        {
            _network = network;
            LocalId = id;
        }

        public ulong LocalId { get; }

        public void Send(ulong to, byte[] data, int length) => _network.Send(LocalId, to, data, length);

        public bool TryReceive(byte[] buffer, out int length, out ulong from) => _network.TryReceive(LocalId, buffer, out length, out from);
    }
}
