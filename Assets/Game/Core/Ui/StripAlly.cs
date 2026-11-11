using Game.Core.Party;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public readonly struct StripAlly
    {
        public StripAlly(int slot, int colorIndex, PartyPresence presence, string reaction, HeroLook look = default)
        {
            Slot = slot;
            ColorIndex = colorIndex;
            Presence = presence;
            Reaction = reaction;
            Look = look;
        }

        public int Slot { get; }
        public int ColorIndex { get; }
        public PartyPresence Presence { get; }
        public string Reaction { get; }
        public HeroLook Look { get; }
        public bool Faded => Presence == PartyPresence.Offline;
        public bool Swings => Presence == PartyPresence.Fighting;
    }
}
