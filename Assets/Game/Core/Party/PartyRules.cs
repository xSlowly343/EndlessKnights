using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyRules
    {
        public const string FileName = "party.json";
        public const int SupportedVersion = 1;
        public const int MaxSlots = 4;

        public double PierceShare { get; private set; }
        public int PierceKillsPerTick { get; private set; }
        public PierceRules PierceCaps { get; private set; } = new PierceRules();

        private PartyRules(int maxMembers, double hpPerExtraPlayer, double restAfterSeconds, double disconnectGraceSeconds, double rejoinWindowSeconds,
            double lateDamageSeconds, double damageBatchSeconds, double stateBroadcastSeconds, double presenceSeconds, double silenceSeconds, double maxEnemyDamageTaken, PartyBuffRules buffs, int maxMessagesPerSecond,
            PartyContributionRules contribution, PartyCapRules caps, PartySearchRules search, PartyReactionRules reactions, PartyTimerRules timer, double worldSwitchSeconds, PartyGhostRules ghost, PartyRelayRules relay, PartyHelpRules help, int maxWeakenedSpawns, double maxWeakenShare, double maxWeakenGold)
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
            Buffs = buffs;
            MaxMessagesPerSecond = maxMessagesPerSecond;
            Contribution = contribution;
            Caps = caps;
            Search = search;
            Reactions = reactions;
            Timer = timer;
            WorldSwitchSeconds = worldSwitchSeconds;
            Ghost = ghost;
            Relay = relay;
            Help = help;
            MaxWeakenedSpawns = maxWeakenedSpawns;
            MaxWeakenShare = maxWeakenShare;
            MaxWeakenGold = maxWeakenGold;
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
        public PartyBuffRules Buffs { get; }
        public int MaxMessagesPerSecond { get; }
        public PartyContributionRules Contribution { get; }
        public PartyCapRules Caps { get; }
        public PartySearchRules Search { get; }
        public PartyReactionRules Reactions { get; }
        public PartyTimerRules Timer { get; }
        public double WorldSwitchSeconds { get; }
        public PartyGhostRules Ghost { get; }
        public PartyRelayRules Relay { get; }
        public PartyHelpRules Help { get; }
        public PartyGrowthRules Growth { get; private set; } = PartyGrowthRules.None;
        public int MaxWeakenedSpawns { get; }
        public double MaxWeakenShare { get; }
        public double MaxWeakenGold { get; }

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
            var restAfter = presence.Field("restAfterSeconds").AsNumber(1, 86400);
            var contribution = PartyContributionRules.Read(root.Field("contribution"), restAfter);
            return new PartyRules(
                root.Field("maxMembers").AsInt(1, MaxSlots),
                root.Field("hpPerExtraPlayer").AsNumber(0, 100),
                restAfter,
                grace,
                rejoin,
                root.Field("lateDamageSeconds").AsNumber(0, 10),
                packets.Field("damageBatchSeconds").AsNumber(0.1, 10),
                packets.Field("stateBroadcastSeconds").AsNumber(0.1, 10),
                presenceSeconds,
                silence,
                root.Field("maxEnemyDamageTaken").AsNumber(0, 100),
                PartyBuffRules.Read(root.Field("buffs")),
                packets.Field("maxMessagesPerSecond").AsInt(5, 1000),
                contribution,
                PartyCapRules.Read(root.Field("caps")),
                PartySearchRules.Read(root.Field("search")),
                PartyReactionRules.Read(root.Field("reactions")),
                PartyTimerRules.Read(root.Field("timer")),
                root.Field("worldSwitchSeconds").AsNumber(1, 25),
                PartyGhostRules.Read(root.Field("ghost")),
                PartyRelayRules.Read(root.Field("relay")),
                PartyHelpRules.Read(root.Field("help")),
                root.Field("weaken").Field("maxEnemies").AsInt(1, 100),
                root.Field("weaken").Field("maxShare").AsNumber(0, 0.99),
                root.Field("weaken").Field("maxGold").AsNumber(0, 10))
            {
                PierceShare = root.HasField("pierce") ? root.Field("pierce").Field("share").AsNumber(0, 1) : 0,
                PierceKillsPerTick = root.HasField("pierce") ? root.Field("pierce").Field("maxKillsPerTick").AsInt(0, EnemyHistory.Depth / 2) : 0,
                PierceCaps = root.HasField("pierce") ? PierceRules.Read(root.Field("pierce")) : new PierceRules(),
                Growth = root.HasField("growth") ? PartyGrowthRules.Read(root.Field("growth")) : PartyGrowthRules.None
            };
        }
    }
}
