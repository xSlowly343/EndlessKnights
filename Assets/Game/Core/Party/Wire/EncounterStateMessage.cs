namespace Game.Core.Party.Wire
{
    public struct EncounterStateMessage
    {
        public uint Sequence;
        public long Tick;
        public uint EnemyId;
        public PartyEnemyKind EnemyKind;
        public double BarLeft;
        public double BarFull;
        public int BossTicksLeft;
        public byte KillsOnStage;
        public bool Farming;
        public byte WeightMask;
        public double DamageTaken;
        public ulong EnemySeed;

        public void Write(ref PartyWriter writer)
        {
            writer.UInt(Sequence);
            writer.Long(Tick);
            writer.UInt(EnemyId);
            writer.Byte((byte)EnemyKind);
            writer.Double(BarLeft);
            writer.Double(BarFull);
            writer.Int(BossTicksLeft);
            writer.Byte(KillsOnStage);
            writer.Bool(Farming);
            writer.Byte(WeightMask);
            writer.Double(DamageTaken);
            writer.ULong(EnemySeed);
        }

        public static EncounterStateMessage Read(ref PartyReader reader) => new EncounterStateMessage
        {
            Sequence = reader.UInt(),
            Tick = reader.Long(),
            EnemyId = reader.UInt(),
            EnemyKind = (PartyEnemyKind)reader.Byte(),
            BarLeft = reader.Double(),
            BarFull = reader.Double(),
            BossTicksLeft = reader.Int(),
            KillsOnStage = reader.Byte(),
            Farming = reader.Bool(),
            WeightMask = reader.Byte(),
            DamageTaken = reader.Double(),
            EnemySeed = reader.ULong()
        };

        public static EncounterStateMessage Of(PartyEncounter encounter, uint sequence)
        {
            byte weights = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (encounter.Member(slot).Weight > 0)
                    weights |= (byte)(1 << slot);
            }
            return new EncounterStateMessage
            {
                Sequence = sequence,
                Tick = encounter.Tick,
                EnemyId = encounter.EnemyId,
                EnemyKind = encounter.EnemyKind,
                BarLeft = encounter.BarLeft,
                BarFull = encounter.BarFull,
                BossTicksLeft = encounter.BossTicksLeft,
                KillsOnStage = (byte)System.Math.Min(255, encounter.KillsOnStage),
                Farming = encounter.Farming,
                WeightMask = weights,
                DamageTaken = encounter.DamageTakenMultiplier,
                EnemySeed = encounter.EnemySeed
            };
        }
    }
}
