using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class CoopRules
    {
        public const int MaxStats = 48;
        public const int MaxOffered = 16;

        public static readonly StatId EnemyDamageTaken = new StatId("damageTaken");
        public static readonly StatId EnemyTimerSlow = new StatId("bossTimerSlow");

        private static readonly Dictionary<string, PartyGiveMode> ModeNames = new Dictionary<string, PartyGiveMode>(StringComparer.Ordinal)
        {
            ["together"] = PartyGiveMode.Together,
            ["support"] = PartyGiveMode.Support,
            ["solo"] = PartyGiveMode.Solo
        };

        private readonly double[] _modeSelf = new double[3];
        private readonly double[] _modePower = new double[3];

        private CoopRules(IReadOnlyList<CoopStatDef> stats, double enemySeconds, double enemyResendSeconds)
        {
            Stats = stats;
            EnemySeconds = enemySeconds;
            EnemyResendSeconds = enemyResendSeconds;
        }

        public IReadOnlyList<CoopStatDef> Stats { get; }
        public double EnemySeconds { get; }
        public double EnemyResendSeconds { get; }

        public static CoopRules Empty { get; } = new CoopRules(Array.Empty<CoopStatDef>(), 1.5, 1);

        public double ModeSelf(PartyGiveMode mode) => _modeSelf[(int)mode];

        public double ModePower(PartyGiveMode mode) => _modePower[(int)mode];

        public int IndexOf(StatId stat, ModifierKind kind)
        {
            for (var i = 0; i < Stats.Count; i++)
            {
                if (Stats[i].Stat == stat && Stats[i].Kind == kind)
                    return i;
            }
            return -1;
        }

        public static bool TryParseMode(string name, out PartyGiveMode mode) => ModeNames.TryGetValue(name, out mode);

        public static PartyGiveMode ReadMode(ContentNode node)
        {
            if (!TryParseMode(node.AsString(), out var mode))
                throw node.Error($"unknown party mode '{node.AsString()}', expected together, support or solo");
            return mode;
        }

        public static bool IsEnemyStat(StatId stat) => stat == EnemyDamageTaken || stat == EnemyTimerSlow;

        public static CoopRules Read(ContentNode node)
        {
            var stats = new List<CoopStatDef>();
            foreach (var item in node.Field("stats").Items())
            {
                if (stats.Count == MaxStats)
                    throw item.Error($"no more than {MaxStats} party buff stats fit in one offer");
                var stat = new StatId(item.Field("stat").AsString());
                var kind = ModifierReader.ReadKind(item.Field("kind"));
                foreach (var known in stats)
                {
                    if (known.Stat == stat && known.Kind == kind)
                        throw item.Error($"party buff stat '{stat}' with kind '{item.Field("kind").AsString()}' is listed twice");
                }
                stats.Add(new CoopStatDef(stat, kind, item.Field("cap").AsNumber(0, 1000), item.HasField("negate") && item.Field("negate").AsBool()));
            }
            var rules = new CoopRules(stats, node.Field("enemySeconds").AsNumber(0.2, 60), node.Field("enemyResendSeconds").AsNumber(0.1, 60));
            var modes = node.Field("modes");
            foreach (var pair in ModeNames)
            {
                var mode = modes.Field(pair.Key);
                rules._modeSelf[(int)pair.Value] = mode.Field("self").AsNumber(0, 10);
                rules._modePower[(int)pair.Value] = mode.Field("power").AsNumber(0, 10);
            }
            return rules;
        }
    }
}
