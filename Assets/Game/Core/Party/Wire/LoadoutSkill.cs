namespace Game.Core.Party.Wire
{
    public struct LoadoutSkill
    {
        public int GemNetId;
        public byte Level;
        public byte Combo;

        public bool Empty => GemNetId == 0;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(GemNetId);
            writer.Byte(Level);
            writer.Byte(Combo);
        }

        public static LoadoutSkill Read(ref PartyReader reader) => new LoadoutSkill
        {
            GemNetId = reader.Int(),
            Level = reader.Byte(),
            Combo = reader.Byte()
        };
    }
}
