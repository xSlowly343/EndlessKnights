namespace Game.Core.Party.Wire
{
    public struct EnemyKilledMessage
    {
        public long Tick;
        public uint EnemyId;
        public PartyEnemyKind EnemyKind;
        public double Share0;
        public double Share1;
        public double Share2;
        public double Share3;
        public byte Rewarded;
        public float Loop;
        public float SavedSeconds;

        public double Contribution(int slot) => slot switch
        {
            0 => Share0,
            1 => Share1,
            2 => Share2,
            3 => Share3,
            _ => 0
        };

        public bool RewardedFor(int slot) => (Rewarded & (1 << slot)) != 0;

        public void Write(ref PartyWriter writer)
        {
            writer.Long(Tick);
            writer.UInt(EnemyId);
            writer.Byte((byte)EnemyKind);
            writer.Double(Share0);
            writer.Double(Share1);
            writer.Double(Share2);
            writer.Double(Share3);
            writer.Byte(Rewarded);
            writer.Float(Loop);
            writer.Float(SavedSeconds);
        }

        public static EnemyKilledMessage Read(ref PartyReader reader) => new EnemyKilledMessage
        {
            Tick = reader.Long(),
            EnemyId = reader.UInt(),
            EnemyKind = (PartyEnemyKind)reader.Byte(),
            Share0 = reader.Double(),
            Share1 = reader.Double(),
            Share2 = reader.Double(),
            Share3 = reader.Double(),
            Rewarded = reader.Byte(),
            Loop = reader.Float(),
            SavedSeconds = reader.Float()
        };

        public static EnemyKilledMessage Of(in PartyEvent killed) => new EnemyKilledMessage
        {
            Tick = killed.Tick,
            EnemyId = killed.EnemyId,
            EnemyKind = killed.EnemyKind,
            Share0 = killed.Share0,
            Share1 = killed.Share1,
            Share2 = killed.Share2,
            Share3 = killed.Share3,
            Rewarded = killed.Rewarded,
            Loop = (float)killed.Loop,
            SavedSeconds = (float)killed.SavedSeconds
        };
    }
}
