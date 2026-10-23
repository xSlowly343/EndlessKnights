using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyRules
    {
        public const string FileName = "party.json";
        public const int SupportedVersion = 1;
        public const int MaxSlots = 4;

        private PartyRules(int maxMembers, double hpPerExtraPlayer, double restAfterSeconds, double disconnectGraceSeconds, double rejoinWindowSeconds,
            double lateDamageSeconds, double damageBatchSeconds, double stateBroadcastSeconds, double presenceSeconds, double silenceSeconds, double maxEnemyDamageTaken)
        {
            MaxMembers = maxMembers;
            HpPerExtraPlayer = hpPerExtraPlayer;
            RestAfterSeconds = restAfterSeconds;
            DisconnectGraceSeconds = disconnectGraceSeconds;
            RejoinWindowSeconds = rejoinWindowSeconds;
            LateDamageSeconds = lateDamageSeconds;
            DamageBatchSeconds = damageBatchSeconds;
            StateBroadcastSeconds = stateBroadcastSeconds;
            PresenceSeconds = presenceSeconds;
            SilenceSeconds = silenceSeconds;
            MaxEnemyDamageTaken = maxEnemyDamageTaken;
        }

        public int MaxMembers { get; }
        public double HpPerExtraPlayer { get; }
        public double RestAfterSeconds { get; }
        public double DisconnectGraceSeconds { get; }
        public double RejoinWindowSeconds { get; }
        public double LateDamageSeconds { get; }
        public double DamageBatchSeconds { get; }
        public double StateBroadcastSeconds { get; }
        public double PresenceSeconds { get; }
        public double SilenceSeconds { get; }
        public double MaxEnemyDamageTaken { get; }

        public double HpMultiplier(int fighters) => System.Math.Max(1, 1 + HpPerExtraPlayer * (System.Math.Max(1, fighters) - 1));

        public static PartyRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var presence = root.Field("presence");
            var grace = presence.Field("disconnectGraceSeconds").AsNumber(1, 3600);
            var rejoin = presence.Field("rejoinWindowSeconds").AsNumber(1, 86400);
            if (rejoin < grace)
                throw presence.Field("rejoinWindowSeconds").Error($"must not be lower than disconnectGraceSeconds ({grace})");
            var packets = root.Field("packets");
            var presenceSeconds = packets.Field("presenceSeconds").AsNumber(0.1, 60);
            var silence = presence.Field("silenceSeconds").AsNumber(0.1, 3600);
            if (silence <= presenceSeconds)
                throw presence.Field("silenceSeconds").Error($"must be longer than packets.presenceSeconds ({presenceSeconds})");
            if (silence > grace)
                throw presence.Field("silenceSeconds").Error($"must not be longer than disconnectGraceSeconds ({grace})");
            return new PartyRules(
                root.Field("maxMembers").AsInt(1, MaxSlots),
                root.Field("hpPerExtraPlayer").AsNumber(0, 100),
                presence.Field("restAfterSeconds").AsNumber(1, 86400),
                grace,
                rejoin,
                root.Field("lateDamageSeconds").AsNumber(0, 10),
                packets.Field("damageBatchSeconds").AsNumber(0.1, 10),
                packets.Field("stateBroadcastSeconds").AsNumber(0.1, 10),
                presenceSeconds,
                silence,
                root.Field("maxEnemyDamageTaken").AsNumber(0, 100));
        }
    }
}
