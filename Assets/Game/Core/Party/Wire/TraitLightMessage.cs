namespace Game.Core.Party.Wire
{
    public struct TraitLightMessage
    {
        public uint EnemyId;
        public float Light;

        public void Write(ref PartyWriter writer)
        {
            writer.UInt(EnemyId);
            writer.Float(Light);
        }

        public static TraitLightMessage Read(ref PartyReader reader) => new TraitLightMessage
        {
            EnemyId = reader.UInt(),
            Light = reader.Float()
        };
    }
}
