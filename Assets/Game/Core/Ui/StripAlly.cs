using Game.Core.Party;

namespace Game.Core.Ui
{
    public readonly struct StripAlly
    {
        public StripAlly(int slot, int colorIndex, PartyPresence presence, string reaction)
        {
            Slot = slot;
            ColorIndex = colorIndex;
            Presence = presence;
            Reaction = reaction;
        }

        public int Slot { get; }
        public int ColorIndex { get; }
        public PartyPresence Presence { get; }
        public string Reaction { get; }
        public bool Faded => Presence == PartyPresence.Offline;
        public bool Swings => Presence == PartyPresence.Fighting;
    }
}
