namespace Game.Core.Party.Wire
{
    public struct NetPullMessage
    {
        public int EffectId;
        public float Seconds;

        public void Write(ref PartyWriter writer)
        {
            writer.Int(EffectId);
            writer.Float(Seconds);
        }

        public static NetPullMessage Read(ref PartyReader reader) => new NetPullMessage
        {
            EffectId = reader.Int(),
            Seconds = reader.Float()
        };
    }
}
