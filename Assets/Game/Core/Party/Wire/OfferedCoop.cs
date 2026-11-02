namespace Game.Core.Party.Wire
{
    public struct OfferedCoop
    {
        public byte Stat;
        public float Value;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Stat);
            writer.Float(Value);
        }

        public static OfferedCoop Read(ref PartyReader reader) => new OfferedCoop
        {
            Stat = reader.Byte(),
            Value = reader.Float()
        };
    }
}
