namespace Game.Core.Party.Wire
{
    public static class PartyCodec
    {
        public const int MaxPacketBytes = 256;

        public static PartyMessageKind KindOf(byte[] buffer, int length) =>
            length > 0 && buffer[0] <= (byte)PartyMessageKind.PartyLoadout ? (PartyMessageKind)buffer[0] : PartyMessageKind.None;

        public static int Encode(byte[] buffer, HelloMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.Hello);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, DamageBatchMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.DamageBatch);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, CastEffectMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.CastEffect);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, PresenceMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.Presence);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, EncounterStateMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.EncounterState);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, EnemyKilledMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.EnemyKilled);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, EncounterMomentMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.EncounterMoment);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, RosterMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.Roster);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, PartyOfferMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.PartyOffer);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, PartyReactionMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.PartyReaction);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int Encode(byte[] buffer, WorldRequestMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.WorldRequest);
            message.Write(ref writer);
            return writer.Length;
        }

        public static int EncodeBossRequest(byte[] buffer) => Start(buffer, PartyMessageKind.BossRequest).Length;

        public static int Encode(byte[] buffer, MarksMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.Marks);
            message.Write(ref writer);
            return writer.Length;
        }

        public static bool TryDecode(byte[] buffer, int length, out MarksMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.Marks);
            message = MarksMessage.Read(ref reader);
            return reader.Complete;
        }

        public static int Encode(byte[] buffer, SynergyMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.Synergy);
            message.Write(ref writer);
            return writer.Length;
        }

        public static bool TryDecode(byte[] buffer, int length, out SynergyMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.Synergy);
            message = SynergyMessage.Read(ref reader);
            return reader.Complete;
        }

        public static int Encode(byte[] buffer, PartyLoadoutMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.PartyLoadout);
            message.Write(ref writer);
            return writer.Length;
        }

        public static bool TryDecode(byte[] buffer, int length, out PartyLoadoutMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.PartyLoadout);
            message = PartyLoadoutMessage.Read(ref reader);
            return reader.Complete;
        }

        public static int Encode(byte[] buffer, AllyCastMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.AllyCast);
            message.Write(ref writer);
            return writer.Length;
        }

        public static bool TryDecode(byte[] buffer, int length, out AllyCastMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.AllyCast);
            message = AllyCastMessage.Read(ref reader);
            return reader.Complete;
        }

        public static int Encode(byte[] buffer, PartyResourceMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.PartyResource);
            message.Write(ref writer);
            return writer.Length;
        }

        public static bool TryDecode(byte[] buffer, int length, out PartyResourceMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.PartyResource);
            message = PartyResourceMessage.Read(ref reader);
            return reader.Complete;
        }

        public static int Encode(byte[] buffer, FocusRelayMessage message)
        {
            var writer = Start(buffer, PartyMessageKind.FocusRelay);
            message.Write(ref writer);
            return writer.Length;
        }

        public static bool TryDecode(byte[] buffer, int length, out FocusRelayMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.FocusRelay);
            message = FocusRelayMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out WorldRequestMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.WorldRequest);
            message = WorldRequestMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out PartyReactionMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.PartyReaction);
            message = PartyReactionMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out PartyOfferMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.PartyOffer);
            message = PartyOfferMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out HelloMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.Hello);
            message = HelloMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out DamageBatchMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.DamageBatch);
            message = DamageBatchMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out CastEffectMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.CastEffect);
            message = CastEffectMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out PresenceMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.Presence);
            message = PresenceMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out EncounterStateMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.EncounterState);
            message = EncounterStateMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out EnemyKilledMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.EnemyKilled);
            message = EnemyKilledMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out EncounterMomentMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.EncounterMoment);
            message = EncounterMomentMessage.Read(ref reader);
            return reader.Complete;
        }

        public static bool TryDecode(byte[] buffer, int length, out RosterMessage message)
        {
            var reader = Open(buffer, length, PartyMessageKind.Roster);
            message = RosterMessage.Read(ref reader);
            return reader.Complete;
        }

        private static PartyWriter Start(byte[] buffer, PartyMessageKind kind)
        {
            var writer = new PartyWriter(buffer);
            writer.Byte((byte)kind);
            return writer;
        }

        private static PartyReader Open(byte[] buffer, int length, PartyMessageKind kind)
        {
            var reader = new PartyReader(buffer, length);
            if (reader.Byte() != (byte)kind)
                reader = new PartyReader(buffer, 0);
            return reader;
        }
    }
}
