using Game.Core.Party;

namespace Game.Core.Ui
{
    public readonly struct PartyRow
    {
        public PartyRow(bool isSelf, int slot, string name, string details, PartyPresence presence, string reaction = "")
        {
            Reaction = reaction;
            IsSelf = isSelf;
            IsFree = false;
            Slot = slot;
            Name = name;
            Details = details;
            Presence = presence;
        }

        private PartyRow(bool isFree)
        {
            IsSelf = false;
            IsFree = isFree;
            Slot = PartyEncounter.NoSlot;
            Reaction = string.Empty;
            Name = string.Empty;
            Details = string.Empty;
            Presence = PartyPresence.Offline;
        }

        public static PartyRow Free => new PartyRow(true);

        public bool IsSelf { get; }
        public bool IsFree { get; }
        public int Slot { get; }
        public string Reaction { get; }
        public bool CanBeReported => !IsSelf && !IsFree && Slot >= 0;
        public string Name { get; }
        public string Details { get; }
        public PartyPresence Presence { get; }
    }
}
