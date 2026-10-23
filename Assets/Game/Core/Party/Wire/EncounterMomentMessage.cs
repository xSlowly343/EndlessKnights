namespace Game.Core.Party.Wire
{
    public struct EncounterMomentMessage
    {
        public PartyEventKind Kind;
        public long Tick;
        public uint EnemyId;
        public int BossTicksLeft;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte((byte)Kind);
            writer.Long(Tick);
            writer.UInt(EnemyId);
            writer.Int(BossTicksLeft);
        }

        public static EncounterMomentMessage Read(ref PartyReader reader) => new EncounterMomentMessage
        {
            Kind = (PartyEventKind)reader.Byte(),
            Tick = reader.Long(),
            EnemyId = reader.UInt(),
            BossTicksLeft = reader.Int()
        };
    }
}
