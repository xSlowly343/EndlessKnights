namespace Game.Core.Party.Wire
{
    public struct SynergyMessage
    {
        public uint Constellations;

        public void Write(ref PartyWriter writer) => writer.UInt(Constellations);

        public static SynergyMessage Read(ref PartyReader reader) => new SynergyMessage { Constellations = reader.UInt() };
    }
}
