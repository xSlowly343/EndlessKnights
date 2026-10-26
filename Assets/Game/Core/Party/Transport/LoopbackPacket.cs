namespace Game.Core.Party.Transport
{
    internal struct LoopbackPacket
    {
        public bool InFlight;
        public double DeliverAtMs;
        public ulong From;
        public ulong To;
        public int Length;
    }
}
