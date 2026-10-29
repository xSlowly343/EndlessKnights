namespace Game.Core.Party.Wire
{
    public struct FocusRelayMessage
    {
        public const byte Nobody = byte.MaxValue;

        public byte From;
        public byte To;
        public float Focus;

        public static FocusRelayMessage Dropped(double focus) => new FocusRelayMessage { From = Nobody, To = Nobody, Focus = (float)focus };

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(From);
            writer.Byte(To);
            writer.Float(Focus);
        }

        public static FocusRelayMessage Read(ref PartyReader reader) => new FocusRelayMessage
        {
            From = reader.Byte(),
            To = reader.Byte(),
            Focus = reader.Float()
        };
    }
}
