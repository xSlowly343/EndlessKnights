namespace Game.Core.Party.Wire
{
    public struct HelloMessage
    {
        public byte HeroClass;
        public long Stage;
        public double SharePerSecondCap;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(HeroClass);
            writer.Long(Stage);
            writer.Double(SharePerSecondCap);
        }

        public static HelloMessage Read(ref PartyReader reader) => new HelloMessage
        {
            HeroClass = reader.Byte(),
            Stage = reader.Long(),
            SharePerSecondCap = reader.Double()
        };
    }
}
