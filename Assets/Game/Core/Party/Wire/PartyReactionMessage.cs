namespace Game.Core.Party.Wire
{
    public struct PartyReactionMessage
    {
        public byte Slot;
        public byte Reaction;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            writer.Byte(Reaction);
        }

        public static PartyReactionMessage Read(ref PartyReader reader) => new PartyReactionMessage
        {
            Slot = reader.Byte(),
            Reaction = reader.Byte()
        };
    }
}
