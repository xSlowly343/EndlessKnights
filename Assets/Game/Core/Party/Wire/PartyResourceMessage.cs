namespace Game.Core.Party.Wire
{
    public struct PartyResourceMessage
    {
        public byte Slot;
        public float Amount;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            writer.Float(Amount);
        }

        public static PartyResourceMessage Read(ref PartyReader reader) => new PartyResourceMessage
        {
            Slot = reader.Byte(),
            Amount = reader.Float()
        };
    }
}
