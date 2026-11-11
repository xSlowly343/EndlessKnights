namespace Game.Core.Party.Wire
{
    public struct PartyKeysMessage
    {
        public const int MaxKeys = 8;

        public byte Slot;
        public byte Count;
        public long Key0;
        public long Key1;
        public long Key2;
        public long Key3;
        public long Key4;
        public long Key5;
        public long Key6;
        public long Key7;

        public long Key(int index) => index switch
        {
            0 => Key0,
            1 => Key1,
            2 => Key2,
            3 => Key3,
            4 => Key4,
            5 => Key5,
            6 => Key6,
            7 => Key7,
            _ => 0
        };

        public void SetKey(int index, long key)
        {
            switch (index)
            {
                case 0: Key0 = key; break;
                case 1: Key1 = key; break;
                case 2: Key2 = key; break;
                case 3: Key3 = key; break;
                case 4: Key4 = key; break;
                case 5: Key5 = key; break;
                case 6: Key6 = key; break;
                case 7: Key7 = key; break;
            }
        }

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            writer.Byte(Count);
            for (var i = 0; i < MaxKeys; i++)
                writer.Long(Key(i));
        }

        public static PartyKeysMessage Read(ref PartyReader reader)
        {
            var message = new PartyKeysMessage { Slot = reader.Byte(), Count = reader.Byte() };
            for (var i = 0; i < MaxKeys; i++)
                message.SetKey(i, reader.Long());
            if (message.Count > MaxKeys)
                message.Count = MaxKeys;
            return message;
        }
    }
}
