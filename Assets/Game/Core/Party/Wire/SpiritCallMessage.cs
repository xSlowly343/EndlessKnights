namespace Game.Core.Party.Wire
{
    public struct SpiritCallMessage
    {
        public byte Count;
        public float Hp;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Count);
            writer.Float(Hp);
        }

        public static SpiritCallMessage Read(ref PartyReader reader) => new SpiritCallMessage
        {
            Count = reader.Byte(),
            Hp = reader.Float()
        };
    }
}
