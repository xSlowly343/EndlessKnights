namespace Game.Core.Party.Wire
{
    public struct SynergyMessage
    {
        public uint Reactions;

        public void Write(ref PartyWriter writer) => writer.UInt(Reactions);

        public static SynergyMessage Read(ref PartyReader reader) => new SynergyMessage { Reactions = reader.UInt() };
    }
}
