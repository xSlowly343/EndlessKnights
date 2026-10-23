namespace Game.Core.Party.Wire
{
    public struct RosterMessage
    {
        public uint Version;
        public ulong Player0;
        public ulong Player1;
        public ulong Player2;
        public ulong Player3;
        public byte Presence0;
        public byte Presence1;
        public byte Presence2;
        public byte Presence3;

        public ulong Player(int slot) => slot switch
        {
            0 => Player0,
            1 => Player1,
            2 => Player2,
            3 => Player3,
            _ => 0
        };

        public PartyPresence Presence(int slot) => (PartyPresence)(slot switch
        {
            0 => Presence0,
            1 => Presence1,
            2 => Presence2,
            3 => Presence3,
            _ => (byte)PartyPresence.Offline
        });

        public int SlotOf(ulong playerId)
        {
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (playerId != 0 && Player(slot) == playerId)
                    return slot;
            }
            return PartyEncounter.NoSlot;
        }

        public void Write(ref PartyWriter writer)
        {
            writer.UInt(Version);
            writer.ULong(Player0);
            writer.ULong(Player1);
            writer.ULong(Player2);
            writer.ULong(Player3);
            writer.Byte(Presence0);
            writer.Byte(Presence1);
            writer.Byte(Presence2);
            writer.Byte(Presence3);
        }

        public static RosterMessage Read(ref PartyReader reader) => new RosterMessage
        {
            Version = reader.UInt(),
            Player0 = reader.ULong(),
            Player1 = reader.ULong(),
            Player2 = reader.ULong(),
            Player3 = reader.ULong(),
            Presence0 = reader.Byte(),
            Presence1 = reader.Byte(),
            Presence2 = reader.Byte(),
            Presence3 = reader.Byte()
        };

        public static RosterMessage Of(PartyEncounter encounter, uint version)
        {
            ulong Id(int slot) => encounter.Member(slot).Joined ? encounter.Member(slot).PlayerId : 0;
            byte State(int slot) => (byte)(encounter.Member(slot).Joined ? encounter.Presence(slot) : PartyPresence.Offline);
            return new RosterMessage
            {
                Version = version,
                Player0 = Id(0),
                Player1 = Id(1),
                Player2 = Id(2),
                Player3 = Id(3),
                Presence0 = State(0),
                Presence1 = State(1),
                Presence2 = State(2),
                Presence3 = State(3)
            };
        }
    }
}
