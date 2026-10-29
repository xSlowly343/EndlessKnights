namespace Game.Core.Party.Wire
{
    public struct PresenceMessage
    {
        public PartyPresence Presence;
        public long Stage;
        public float Resource;
        public float Contribution;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte((byte)Presence);
            writer.Long(Stage);
            writer.Float(Resource);
            writer.Float(Contribution);
        }

        public static PresenceMessage Read(ref PartyReader reader) => new PresenceMessage
        {
            Presence = (PartyPresence)reader.Byte(),
            Stage = reader.Long(),
            Resource = reader.Float(),
            Contribution = reader.Float()
        };
    }
}
