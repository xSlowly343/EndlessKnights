namespace Game.Core.Party.Wire
{
    public struct TrialStateMessage
    {
        public const byte Nobody = byte.MaxValue;

        public long Tick;
        public long TrialId;
        public PartyTrialOutcome Outcome;
        public int ResultTier;
        public double HpLeft;
        public double HpFull;
        public float Shield0;
        public float Shield1;
        public float Shield2;
        public float Shield3;
        public float SkillShield;
        public byte Holder;
        public byte Resisted;
        public byte Chain;
        public byte Wave;
        public byte WavesCleared;
        public bool PulseOpen;
        public int TicksLeft;

        public readonly float Shield(PartyTrialDamage kind) => kind switch
        {
            PartyTrialDamage.Keys => Shield0,
            PartyTrialDamage.Mouse => Shield1,
            PartyTrialDamage.Skills => Shield2,
            _ => Shield3
        };

        public static TrialStateMessage Of(PartyEncounter encounter)
        {
            var fight = encounter.TrialFight;
            return new TrialStateMessage
            {
                Tick = encounter.Tick,
                TrialId = encounter.TrialId,
                Outcome = fight.Outcome,
                ResultTier = fight.ResultTier,
                HpLeft = fight.HpLeft,
                HpFull = fight.HpFull,
                Shield0 = (float)fight.ShieldLeft(PartyTrialDamage.Keys),
                Shield1 = (float)fight.ShieldLeft(PartyTrialDamage.Mouse),
                Shield2 = (float)fight.ShieldLeft(PartyTrialDamage.Skills),
                Shield3 = (float)fight.ShieldLeft(PartyTrialDamage.Minions),
                SkillShield = (float)fight.SkillShieldLeft,
                Holder = Small(encounter.TrialHolderSlot),
                Resisted = Small(fight.Resisted),
                Chain = Small(fight.Chain),
                Wave = Small(fight.Wave),
                WavesCleared = Small(fight.WavesCleared),
                PulseOpen = fight.PulseOpen,
                TicksLeft = (int)System.Math.Max(0, fight.TotalTicks - fight.Tick)
            };
        }

        private static byte Small(int value) => value < 0 ? Nobody : (byte)System.Math.Min(Nobody - 1, value);

        public void Write(ref PartyWriter writer)
        {
            writer.Long(Tick);
            writer.Long(TrialId);
            writer.Byte((byte)Outcome);
            writer.Int(ResultTier);
            writer.Double(HpLeft);
            writer.Double(HpFull);
            writer.Float(Shield0);
            writer.Float(Shield1);
            writer.Float(Shield2);
            writer.Float(Shield3);
            writer.Float(SkillShield);
            writer.Byte(Holder);
            writer.Byte(Resisted);
            writer.Byte(Chain);
            writer.Byte(Wave);
            writer.Byte(WavesCleared);
            writer.Bool(PulseOpen);
            writer.Int(TicksLeft);
        }

        public static TrialStateMessage Read(ref PartyReader reader) => new TrialStateMessage
        {
            Tick = reader.Long(),
            TrialId = reader.Long(),
            Outcome = (PartyTrialOutcome)reader.Byte(),
            ResultTier = reader.Int(),
            HpLeft = reader.Double(),
            HpFull = reader.Double(),
            Shield0 = reader.Float(),
            Shield1 = reader.Float(),
            Shield2 = reader.Float(),
            Shield3 = reader.Float(),
            SkillShield = reader.Float(),
            Holder = reader.Byte(),
            Resisted = reader.Byte(),
            Chain = reader.Byte(),
            Wave = reader.Byte(),
            WavesCleared = reader.Byte(),
            PulseOpen = reader.Bool(),
            TicksLeft = reader.Int()
        };
    }
}
