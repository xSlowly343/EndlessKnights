using Game.Core.Sim;

namespace Game.Core.Party.Wire
{
    public struct PartyLookMessage
    {
        public byte Slot;
        public byte Cloak;
        public byte Eyes;
        public byte Strike;

        public HeroLook Look => new HeroLook(Cloak, Eyes, Strike);

        public static PartyLookMessage Of(HeroLook look) => new PartyLookMessage { Cloak = look.Cloak, Eyes = look.Eyes, Strike = look.Strike };

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            writer.Byte(Cloak);
            writer.Byte(Eyes);
            writer.Byte(Strike);
        }

        public static PartyLookMessage Read(ref PartyReader reader) => new PartyLookMessage
        {
            Slot = reader.Byte(),
            Cloak = reader.Byte(),
            Eyes = reader.Byte(),
            Strike = reader.Byte()
        };
    }
}
