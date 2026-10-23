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
        public ShownEffect Effect0;
        public ShownEffect Effect1;
        public ShownEffect Effect2;
        public ShownEffect Effect3;

        public ShownEffect Effect(int index) => index switch
        {
            0 => Effect0,
            1 => Effect1,
            2 => Effect2,
            3 => Effect3,
            _ => default
        };

        private void SetEffect(int index, ShownEffect effect)
        {
            switch (index)
            {
                case 0: Effect0 = effect; break;
                case 1: Effect1 = effect; break;
                case 2: Effect2 = effect; break;
                case 3: Effect3 = effect; break;
            }
        }

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
            for (var i = 0; i < ShownEffect.MaxShown; i++)
                Effect(i).Write(ref writer);
        }

        public static EncounterStateMessage Read(ref PartyReader reader)
        {
            var message = ReadFields(ref reader);
            for (var i = 0; i < ShownEffect.MaxShown; i++)
                message.SetEffect(i, ShownEffect.Read(ref reader));
            return message;
        }

        private static EncounterStateMessage ReadFields(ref PartyReader reader) => new EncounterStateMessage
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
            var message = new EncounterStateMessage
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
            var shown = 0;
            for (var i = 0; i < PartyEncounter.MaxEffects && shown < ShownEffect.MaxShown; i++)
            {
                var effect = encounter.Effect(i);
                if (effect.Active)
                    message.SetEffect(shown++, new ShownEffect { Id = effect.Id, TicksLeft = effect.TicksLeft });
            }
            return message;
        }
    }
}
