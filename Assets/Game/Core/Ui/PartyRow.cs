using Game.Core.Party;

namespace Game.Core.Ui
{
    public readonly struct PartyRow
    {
        public PartyRow(bool isSelf, string name, string details, PartyPresence presence)
        {
            IsSelf = isSelf;
            IsFree = false;
            Name = name;
            Details = details;
            Presence = presence;
        }

        private PartyRow(bool isFree)
        {
            IsSelf = false;
            IsFree = isFree;
            Name = string.Empty;
            Details = string.Empty;
            Presence = PartyPresence.Offline;
        }

        public static PartyRow Free => new PartyRow(true);

        public bool IsSelf { get; }
        public bool IsFree { get; }
        public string Name { get; }
        public string Details { get; }
        public PartyPresence Presence { get; }
    }
}
