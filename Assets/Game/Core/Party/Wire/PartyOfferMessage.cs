namespace Game.Core.Party.Wire
{
    public struct PartyOfferMessage
    {
        public byte Slot;
        public float Stat0;
        public float Stat1;
        public float Stat2;
        public float Stat3;
        public float Stat4;
        public float Stat5;
        public OfferedAura Aura0;
        public OfferedAura Aura1;
        public OfferedAura Aura2;
        public OfferedAura Aura3;
        public OfferedAura Aura4;
        public OfferedAura Aura5;
        public OfferedAura Aura6;
        public OfferedAura Aura7;
        public OfferedAura Aura8;

        public double Stat(int index) => index switch
        {
            0 => Stat0,
            1 => Stat1,
            2 => Stat2,
            3 => Stat3,
            4 => Stat4,
            5 => Stat5,
            _ => 0
        };

        public void SetStat(int index, double value)
        {
            var single = (float)value;
            switch (index)
            {
                case 0: Stat0 = single; break;
                case 1: Stat1 = single; break;
                case 2: Stat2 = single; break;
                case 3: Stat3 = single; break;
                case 4: Stat4 = single; break;
                case 5: Stat5 = single; break;
            }
        }

        public int LongestAuraTicks
        {
            get
            {
                var ticks = 0;
                for (var i = 0; i < PartyBuffRules.MaxAuras; i++)
                {
                    if (Aura(i).Active)
                        ticks = System.Math.Max(ticks, Aura(i).TicksLeft);
                }
                return ticks;
            }
        }

        public OfferedAura Aura(int index) => index switch
        {
            0 => Aura0,
            1 => Aura1,
            2 => Aura2,
            3 => Aura3,
            4 => Aura4,
            5 => Aura5,
            6 => Aura6,
            7 => Aura7,
            8 => Aura8,
            _ => default
        };

        public void SetAura(int index, OfferedAura aura)
        {
            switch (index)
            {
                case 0: Aura0 = aura; break;
                case 1: Aura1 = aura; break;
                case 2: Aura2 = aura; break;
                case 3: Aura3 = aura; break;
                case 4: Aura4 = aura; break;
                case 5: Aura5 = aura; break;
                case 6: Aura6 = aura; break;
                case 7: Aura7 = aura; break;
                case 8: Aura8 = aura; break;
            }
        }

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            for (var i = 0; i < PartyBuffRules.MaxStats; i++)
                writer.Float((float)Stat(i));
            for (var i = 0; i < PartyBuffRules.MaxAuras; i++)
                Aura(i).Write(ref writer);
        }

        public static PartyOfferMessage Read(ref PartyReader reader)
        {
            var message = new PartyOfferMessage { Slot = reader.Byte() };
            for (var i = 0; i < PartyBuffRules.MaxStats; i++)
                message.SetStat(i, reader.Float());
            for (var i = 0; i < PartyBuffRules.MaxAuras; i++)
                message.SetAura(i, OfferedAura.Read(ref reader));
            return message;
        }
    }
}
