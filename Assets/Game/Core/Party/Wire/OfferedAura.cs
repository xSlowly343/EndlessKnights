namespace Game.Core.Party.Wire
{
    public struct OfferedAura
    {
        public int GemNetId;
        public float Strength;
        public int TicksLeft;

        public bool Active => GemNetId != 0 && TicksLeft > 0;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(GemNetId);
            writer.Float(Strength);
            writer.Int(TicksLeft);
        }

        public static OfferedAura Read(ref PartyReader reader) => new OfferedAura
        {
            GemNetId = reader.Int(),
            Strength = reader.Float(),
            TicksLeft = reader.Int()
        };
    }
}
