namespace Game.Core.Party.Wire
{
    public static class PartyCodec
    {
        public const int MaxPacketBytes = 128;

        public static PartyMessageKind KindOf(byte[] buffer, int length) =>
            length > 0 && buffer[0] <= (byte)PartyMessageKind.Roster ? (PartyMessageKind)buffer[0] : PartyMessageKind.None;

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
