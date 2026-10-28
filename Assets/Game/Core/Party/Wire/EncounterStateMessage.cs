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
        public float Recent0;
        public float Recent1;
        public float Recent2;
        public float Recent3;
        public float BossTimerRate;
        public PartyWorld World;
        public byte PendingWorld;
        public byte SwitchTicksLeft;
        public long RiftId;

        public bool Switching => PendingWorld != PartyEncounter.NoWorld;

        public float Recent(int slot) => slot switch
        {
            0 => Recent0,
            1 => Recent1,
            2 => Recent2,
            3 => Recent3,
            _ => 0f
        };

        public void SetRecent(int slot, float share)
        {
            switch (slot)
            {
                case 0: Recent0 = share; break;
                case 1: Recent1 = share; break;
                case 2: Recent2 = share; break;
                case 3: Recent3 = share; break;
            }
        }

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
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                writer.Float(Recent(slot));
            writer.Float(BossTimerRate);
            writer.Byte((byte)World);
            writer.Byte(PendingWorld);
            writer.Byte(SwitchTicksLeft);
            writer.Long(RiftId);
        }

        public static EncounterStateMessage Read(ref PartyReader reader)
        {
            var message = ReadFields(ref reader);
            for (var i = 0; i < ShownEffect.MaxShown; i++)
                message.SetEffect(i, ShownEffect.Read(ref reader));
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                message.SetRecent(slot, reader.Float());
            message.BossTimerRate = reader.Float();
            message.World = (PartyWorld)reader.Byte();
            message.PendingWorld = reader.Byte();
            message.SwitchTicksLeft = reader.Byte();
            message.RiftId = reader.Long();
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
                EnemySeed = encounter.EnemySeed,
                BossTimerRate = encounter.FightingBoss ? (float)encounter.BossTimerRate : 1f,
                World = encounter.World,
                PendingWorld = encounter.PendingWorld,
                SwitchTicksLeft = (byte)System.Math.Min(255, encounter.SwitchTicksLeft),
                RiftId = encounter.RiftId
            };
            var shown = 0;
            for (var i = 0; i < PartyEncounter.MaxEffects && shown < ShownEffect.MaxShown; i++)
            {
                var effect = encounter.Effect(i);
                if (effect.Active)
                    message.SetEffect(shown++, new ShownEffect { Id = effect.Id, TicksLeft = effect.TicksLeft, Owner = effect.Owner });
            }
            return message;
        }
    }
}
