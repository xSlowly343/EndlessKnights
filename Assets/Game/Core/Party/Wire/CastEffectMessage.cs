namespace Game.Core.Party.Wire
{
    public struct CastEffectMessage
    {
        public int EffectId;
        public bool OnEnemy;
        public double Strength;
        public double Seconds;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(EffectId);
            writer.Bool(OnEnemy);
            writer.Double(Strength);
            writer.Double(Seconds);
        }

        public static CastEffectMessage Read(ref PartyReader reader) => new CastEffectMessage
        {
            EffectId = reader.Int(),
            OnEnemy = reader.Bool(),
            Strength = reader.Double(),
            Seconds = reader.Double()
        };
    }
}
