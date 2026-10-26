namespace Game.Core.Party.Wire
{
    public struct ShownEffect
    {
        public const int MaxShown = 4;

        public int Id;
        public int TicksLeft;
        public int Owner;

        public bool Active => Id != 0 && TicksLeft > 0;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(Id);
            writer.Int(TicksLeft);
            writer.Byte(Owner >= 0 && Owner < PartyRules.MaxSlots ? (byte)Owner : byte.MaxValue);
        }

        public static ShownEffect Read(ref PartyReader reader) => new ShownEffect
        {
            Id = reader.Int(),
            TicksLeft = reader.Int(),
            Owner = OwnerOf(reader.Byte())
        };

        private static int OwnerOf(byte value) => value < PartyRules.MaxSlots ? value : PartyEncounter.NoSlot;
    }
}
