using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class CoopLine
    {
        public const string Prefix = "coop|";

        private static readonly Dictionary<string, CoopLine> Registry = new Dictionary<string, CoopLine>(StringComparer.Ordinal);

        private CoopLine(StatId source, CoopTarget to, StatId target, ModifierKind kind)
        {
            Source = source;
            To = to;
            Target = target;
            Kind = kind;
        }

        public StatId Source { get; }
        public CoopTarget To { get; }
        public StatId Target { get; }
        public ModifierKind Kind { get; }
        public CoopSignal PerSignal { get; private set; }
        public double PerStep { get; private set; }
        public int PerCap { get; private set; }
        public CoopSignal AfterSignal { get; private set; }
        public double AfterSeconds { get; private set; }
        public int AfterStacks { get; private set; }
        public StatId? ShareStat { get; private set; }
        public ModifierKind ShareKind { get; private set; }

        public bool HasPer => PerSignal != CoopSignal.None;
        public bool HasAfter => AfterSignal != CoopSignal.None;

        public static bool IsCoop(StatId stat) => stat.Name.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryGet(StatId stat, out CoopLine line)
        {
            lock (Registry)
                return Registry.TryGetValue(stat.Name, out line!);
        }

        public static CoopLine Register(CoopTarget to, StatId target, ModifierKind kind,
            CoopSignal perSignal = CoopSignal.None, double perStep = 0, int perCap = 0,
            CoopSignal afterSignal = CoopSignal.None, double afterSeconds = 0, int afterStacks = 0,
            StatId? shareStat = null, ModifierKind shareKind = ModifierKind.Flat)
        {
            var name = NameOf(to, target, kind, perSignal, perStep, perCap, afterSignal, afterSeconds, afterStacks, shareStat, shareKind);
            lock (Registry)
            {
                if (Registry.TryGetValue(name, out var known))
                    return known;
                var line = new CoopLine(new StatId(name), to, target, kind)
                {
                    PerSignal = perSignal,
                    PerStep = perStep,
                    PerCap = perCap,
                    AfterSignal = afterSignal,
                    AfterSeconds = afterSeconds,
                    AfterStacks = afterStacks,
                    ShareStat = shareStat,
                    ShareKind = shareKind
                };
                Registry.Add(name, line);
                return line;
            }
        }

        private static string NameOf(CoopTarget to, StatId target, ModifierKind kind, CoopSignal perSignal, double perStep, int perCap,
            CoopSignal afterSignal, double afterSeconds, int afterStacks, StatId? shareStat, ModifierKind shareKind)
        {
            var parts = new List<string> { to.ToString(), target.Name, kind.ToString() };
            if (perSignal != CoopSignal.None)
                parts.Add($"per={perSignal},{Number(perStep)},{perCap}");
            if (afterSignal != CoopSignal.None)
                parts.Add($"after={afterSignal},{Number(afterSeconds)},{afterStacks}");
            if (shareStat is { } share)
                parts.Add($"share={share.Name},{shareKind}");
            return Prefix + string.Join("|", parts);
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
