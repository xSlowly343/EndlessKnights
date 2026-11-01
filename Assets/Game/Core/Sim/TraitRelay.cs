namespace Game.Core.Sim
{
    public struct TraitRelay
    {
        public const byte AllSlots = byte.MaxValue;

        public TraitRelayKind Kind;
        public byte To;
        public byte Count;
        public byte Every;
        public byte Max;
        public double Value;
        public double Seconds;
        public double Extra;
    }
}
