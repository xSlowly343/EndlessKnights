namespace Game.Core.Party.Transport
{
    public interface IPartyTransport
    {
        ulong LocalId { get; }
        void Send(ulong to, byte[] data, int length);
        bool TryReceive(byte[] buffer, out int length, out ulong from);
    }
}
