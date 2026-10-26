namespace Game.Core.Party.Wire
{
    public struct DamageBatchMessage
    {
        public uint Sequence;
        public uint EnemyId;
        public double Keys;
        public double Mouse;
        public double Skills;
        public double Dot;
        public double Summons;
        public int Hits;
        public int Crits;

        public double Total => Keys + Mouse + Skills + Dot + Summons;

        public void Write(ref PartyWriter writer)
        {
            writer.UInt(Sequence);
            writer.UInt(EnemyId);
            writer.Double(Keys);
            writer.Double(Mouse);
            writer.Double(Skills);
            writer.Double(Dot);
            writer.Double(Summons);
            writer.Int(Hits);
            writer.Int(Crits);
        }

        public static DamageBatchMessage Read(ref PartyReader reader) => new DamageBatchMessage
        {
            Sequence = reader.UInt(),
            EnemyId = reader.UInt(),
            Keys = reader.Double(),
            Mouse = reader.Double(),
            Skills = reader.Double(),
            Dot = reader.Double(),
            Summons = reader.Double(),
            Hits = reader.Int(),
            Crits = reader.Int()
        };
    }
}
