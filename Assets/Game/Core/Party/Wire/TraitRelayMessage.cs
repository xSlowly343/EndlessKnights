using Game.Core.Sim;

namespace Game.Core.Party.Wire
{
    public struct TraitRelayMessage
    {
        public TraitRelayKind Kind;
        public byte From;
        public byte To;
        public byte Count;
        public byte Every;
        public byte Max;
        public float Value;
        public float Seconds;
        public float Extra;

        public bool Reaches(int slot) => To == TraitRelay.AllSlots || To == slot;

        public TraitRelay Into() => new TraitRelay { Kind = Kind, To = To, Count = Count, Every = Every, Max = Max, Value = Value, Seconds = Seconds, Extra = Extra };

        public void Write(ref PartyWriter writer)
        {
            writer.Byte((byte)Kind);
            writer.Byte(From);
            writer.Byte(To);
            writer.Byte(Count);
            writer.Byte(Every);
            writer.Byte(Max);
            writer.Float(Value);
            writer.Float(Seconds);
            writer.Float(Extra);
        }

        public static TraitRelayMessage Read(ref PartyReader reader) => new TraitRelayMessage
        {
            Kind = (TraitRelayKind)reader.Byte(),
            From = reader.Byte(),
            To = reader.Byte(),
            Count = reader.Byte(),
            Every = reader.Byte(),
            Max = reader.Byte(),
            Value = reader.Float(),
            Seconds = reader.Float(),
            Extra = reader.Float()
        };
    }
}
