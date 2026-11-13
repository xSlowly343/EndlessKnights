using Game.Core.Sim;

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
        public byte Class0;
        public byte Class1;
        public byte Class2;
        public byte Class3;
        public uint RestBank0;
        public uint RestBank1;
        public uint RestBank2;
        public uint RestBank3;
        public byte Guests;
        public uint Echo0;
        public uint Echo1;
        public uint Echo2;
        public uint Echo3;
        public byte Pastime0;
        public byte Pastime1;
        public byte Pastime2;
        public byte Pastime3;

        public PartyPastime Pastime(int slot) => PartyPastime.Unpack(slot switch
        {
            0 => Pastime0,
            1 => Pastime1,
            2 => Pastime2,
            3 => Pastime3,
            _ => (byte)0
        });

        public uint EchoLines(int slot) => slot switch
        {
            0 => Echo0,
            1 => Echo1,
            2 => Echo2,
            3 => Echo3,
            _ => 0
        };

        public bool Guest(int slot) => slot >= 0 && slot < PartyRules.MaxSlots && (Guests & (1 << slot)) != 0;

        public uint RestBankSeconds(int slot) => slot switch
        {
            0 => RestBank0,
            1 => RestBank1,
            2 => RestBank2,
            3 => RestBank3,
            _ => 0
        };

        public byte HeroClass(int slot) => slot switch
        {
            0 => Class0,
            1 => Class1,
            2 => Class2,
            3 => Class3,
            _ => 0
        };

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
            writer.Byte(Class0);
            writer.Byte(Class1);
            writer.Byte(Class2);
            writer.Byte(Class3);
            writer.UInt(RestBank0);
            writer.UInt(RestBank1);
            writer.UInt(RestBank2);
            writer.UInt(RestBank3);
            writer.Byte(Guests);
            writer.UInt(Echo0);
            writer.UInt(Echo1);
            writer.UInt(Echo2);
            writer.UInt(Echo3);
            writer.Byte(Pastime0);
            writer.Byte(Pastime1);
            writer.Byte(Pastime2);
            writer.Byte(Pastime3);
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
            Presence3 = reader.Byte(),
            Class0 = reader.Byte(),
            Class1 = reader.Byte(),
            Class2 = reader.Byte(),
            Class3 = reader.Byte(),
            RestBank0 = reader.UInt(),
            RestBank1 = reader.UInt(),
            RestBank2 = reader.UInt(),
            RestBank3 = reader.UInt(),
            Guests = reader.Byte(),
            Echo0 = reader.UInt(),
            Echo1 = reader.UInt(),
            Echo2 = reader.UInt(),
            Echo3 = reader.UInt(),
            Pastime0 = reader.Byte(),
            Pastime1 = reader.Byte(),
            Pastime2 = reader.Byte(),
            Pastime3 = reader.Byte()
        };

        public static RosterMessage Of(PartyEncounter encounter, uint version)
        {
            ulong Id(int slot) => encounter.Member(slot).Joined ? encounter.Member(slot).PlayerId : 0;
            byte Class(int slot) => encounter.Member(slot).Joined ? encounter.Member(slot).HeroClass : (byte)0;
            byte State(int slot) => (byte)(encounter.Member(slot).Joined ? encounter.Presence(slot) : PartyPresence.Offline);
            var guests = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (encounter.Member(slot).Joined && encounter.Member(slot).Guest)
                    guests |= 1 << slot;
            }
            byte Pastime(int slot) => encounter.Member(slot).Joined && encounter.Presence(slot) == PartyPresence.Peaceful ? encounter.Member(slot).Pastime : (byte)0;
            uint Echo(int slot) => encounter.Member(slot).Joined ? encounter.Member(slot).EchoLines : 0;
            uint Bank(int slot) => encounter.Member(slot).Joined ? (uint)(encounter.Member(slot).RestBankTicks / GameLoop.TicksPerSecond) : 0;
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
                Presence3 = State(3),
                Class0 = Class(0),
                Class1 = Class(1),
                Class2 = Class(2),
                Class3 = Class(3),
                RestBank0 = Bank(0),
                RestBank1 = Bank(1),
                RestBank2 = Bank(2),
                RestBank3 = Bank(3),
                Guests = (byte)guests,
                Echo0 = Echo(0),
                Echo1 = Echo(1),
                Echo2 = Echo(2),
                Echo3 = Echo(3),
                Pastime0 = Pastime(0),
                Pastime1 = Pastime(1),
                Pastime2 = Pastime(2),
                Pastime3 = Pastime(3)
            };
        }
    }
}
