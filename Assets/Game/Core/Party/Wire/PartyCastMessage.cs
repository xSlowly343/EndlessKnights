namespace Game.Core.Party.Wire
{
    public struct PartyCastMessage
    {
        public byte Slot;
        public int GemNetId;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            writer.Int(GemNetId);
        }

        public static PartyCastMessage Read(ref PartyReader reader) => new PartyCastMessage
        {
            Slot = reader.Byte(),
            GemNetId = reader.Int()
        };
    }
}
