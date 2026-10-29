namespace Game.Core.Party.Wire
{
    public struct AllyCastMessage
    {
        public byte Slot;
        public int GemNetId;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            writer.Int(GemNetId);
        }

        public static AllyCastMessage Read(ref PartyReader reader) => new AllyCastMessage
        {
            Slot = reader.Byte(),
            GemNetId = reader.Int()
        };
    }
}
