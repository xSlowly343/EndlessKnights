namespace Game.Core.Party.Wire
{
    public struct PresenceMessage
    {
        public PartyPresence Presence;
        public long Stage;
        public float Resource;
        public float Contribution;
        public float PierceShare;
        public byte EnemySkip;
        public float SplashShare;
        public byte SplashTargets;
        public float BossPierce;
        public float StageCarry;

        public PartyPierceStats Pierce => new PartyPierceStats(PierceShare, EnemySkip, SplashShare, SplashTargets, BossPierce, StageCarry);

        public void Write(ref PartyWriter writer)
        {
            writer.Byte((byte)Presence);
            writer.Long(Stage);
            writer.Float(Resource);
            writer.Float(Contribution);
            writer.Float(PierceShare);
            writer.Byte(EnemySkip);
            writer.Float(SplashShare);
            writer.Byte(SplashTargets);
            writer.Float(BossPierce);
            writer.Float(StageCarry);
        }

        public static PresenceMessage Read(ref PartyReader reader) => new PresenceMessage
        {
            Presence = (PartyPresence)reader.Byte(),
            Stage = reader.Long(),
            Resource = reader.Float(),
            Contribution = reader.Float(),
            PierceShare = reader.Float(),
            EnemySkip = reader.Byte(),
            SplashShare = reader.Float(),
            SplashTargets = reader.Byte(),
            BossPierce = reader.Float(),
            StageCarry = reader.Float()
        };
    }
}
