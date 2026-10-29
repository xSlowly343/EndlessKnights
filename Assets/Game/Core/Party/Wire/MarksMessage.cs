namespace Game.Core.Party.Wire
{
    public struct MarksMessage
    {
        public uint Marks;

        public void Write(ref PartyWriter writer) => writer.UInt(Marks);

        public static MarksMessage Read(ref PartyReader reader) => new MarksMessage { Marks = reader.UInt() };
    }
}
