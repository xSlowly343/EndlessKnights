namespace Game.Core.Party.Wire
{
    public struct CastEffectMessage
    {
        public int EffectId;
        public bool OnEnemy;
        public double Strength;
        public double Seconds;
        public double TimerSlow;
        public double RepeatSeconds;
        public byte WeakenEnemies;
        public double WeakenShare;
        public double WeakenGold;
        public bool End;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(EffectId);
            writer.Bool(OnEnemy);
            writer.Double(Strength);
            writer.Double(Seconds);
            writer.Double(TimerSlow);
            writer.Double(RepeatSeconds);
            writer.Byte(WeakenEnemies);
            writer.Double(WeakenShare);
            writer.Double(WeakenGold);
            writer.Bool(End);
        }

        public static CastEffectMessage Read(ref PartyReader reader) => new CastEffectMessage
        {
            EffectId = reader.Int(),
            OnEnemy = reader.Bool(),
            Strength = reader.Double(),
            Seconds = reader.Double(),
            TimerSlow = reader.Double(),
            RepeatSeconds = reader.Double(),
            WeakenEnemies = reader.Byte(),
            WeakenShare = reader.Double(),
            WeakenGold = reader.Double(),
            End = reader.Bool()
        };
    }
}
