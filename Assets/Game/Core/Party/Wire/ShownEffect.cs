namespace Game.Core.Party.Wire
{
    public struct ShownEffect
    {
        public const int MaxShown = 4;

        public int Id;
        public int TicksLeft;

        public bool Active => Id != 0 && TicksLeft > 0;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(Id);
            writer.Int(TicksLeft);
        }

        public static ShownEffect Read(ref PartyReader reader) => new ShownEffect
        {
            Id = reader.Int(),
            TicksLeft = reader.Int()
        };
    }
}
