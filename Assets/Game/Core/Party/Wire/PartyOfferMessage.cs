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
        public float Stat6;
        public OfferedAura Aura0;
        public OfferedAura Aura1;
        public OfferedAura Aura2;
        public OfferedAura Aura3;
        public OfferedAura Aura4;
        public OfferedAura Aura5;
        public OfferedAura Aura6;
        public OfferedAura Aura7;
        public OfferedAura Aura8;
        public byte CoopCount;
        public OfferedCoop Coop0;
        public OfferedCoop Coop1;
        public OfferedCoop Coop2;
        public OfferedCoop Coop3;
        public OfferedCoop Coop4;
        public OfferedCoop Coop5;
        public OfferedCoop Coop6;
        public OfferedCoop Coop7;
        public OfferedCoop Coop8;
        public OfferedCoop Coop9;
        public OfferedCoop Coop10;
        public OfferedCoop Coop11;
        public OfferedCoop Coop12;
        public OfferedCoop Coop13;
        public OfferedCoop Coop14;
        public OfferedCoop Coop15;

        public double Stat(int index) => index switch
        {
            0 => Stat0,
            1 => Stat1,
            2 => Stat2,
            3 => Stat3,
            4 => Stat4,
            5 => Stat5,
            6 => Stat6,
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
                case 6: Stat6 = single; break;
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

        public OfferedCoop Coop(int index) => index switch
        {
            0 => Coop0,
            1 => Coop1,
            2 => Coop2,
            3 => Coop3,
            4 => Coop4,
            5 => Coop5,
            6 => Coop6,
            7 => Coop7,
            8 => Coop8,
            9 => Coop9,
            10 => Coop10,
            11 => Coop11,
            12 => Coop12,
            13 => Coop13,
            14 => Coop14,
            15 => Coop15,
            _ => default
        };

        public void SetCoop(int index, OfferedCoop coop)
        {
            switch (index)
            {
                case 0: Coop0 = coop; break;
                case 1: Coop1 = coop; break;
                case 2: Coop2 = coop; break;
                case 3: Coop3 = coop; break;
                case 4: Coop4 = coop; break;
                case 5: Coop5 = coop; break;
                case 6: Coop6 = coop; break;
                case 7: Coop7 = coop; break;
                case 8: Coop8 = coop; break;
                case 9: Coop9 = coop; break;
                case 10: Coop10 = coop; break;
                case 11: Coop11 = coop; break;
                case 12: Coop12 = coop; break;
                case 13: Coop13 = coop; break;
                case 14: Coop14 = coop; break;
                case 15: Coop15 = coop; break;
            }
        }

        public bool AddCoop(int stat, double value)
        {
            if (CoopCount >= CoopRules.MaxOffered || stat < 0 || stat > byte.MaxValue)
                return false;
            SetCoop(CoopCount++, new OfferedCoop { Stat = (byte)stat, Value = (float)value });
            return true;
        }

        public void Write(ref PartyWriter writer)
        {
            writer.Byte(Slot);
            for (var i = 0; i < PartyBuffRules.MaxStats; i++)
                writer.Float((float)Stat(i));
            for (var i = 0; i < PartyBuffRules.MaxAuras; i++)
                Aura(i).Write(ref writer);
            writer.Byte(CoopCount);
            for (var i = 0; i < CoopCount; i++)
                Coop(i).Write(ref writer);
        }

        public static PartyOfferMessage Read(ref PartyReader reader)
        {
            var message = new PartyOfferMessage { Slot = reader.Byte() };
            for (var i = 0; i < PartyBuffRules.MaxStats; i++)
                message.SetStat(i, reader.Float());
            for (var i = 0; i < PartyBuffRules.MaxAuras; i++)
                message.SetAura(i, OfferedAura.Read(ref reader));
            var count = reader.Byte();
            if (count > CoopRules.MaxOffered)
                return message;
            message.CoopCount = count;
            for (var i = 0; i < count; i++)
                message.SetCoop(i, OfferedCoop.Read(ref reader));
            return message;
        }
    }
}
