using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public enum PartyUnlockKind
    {
        Mascot,
        Colossus,
        Trials,
        Tree,
        Banner,
        PartySets,
        Alliance,
        BannerSlot,
        TrialTier,
        ColossusTier,
        Look
    }

    public readonly struct PartyUnlock
    {
        public PartyUnlock(int level, PartyUnlockKind kind)
        {
            Level = level;
            Kind = kind;
        }

        public int Level { get; }
        public PartyUnlockKind Kind { get; }
    }

    public readonly struct PartyLevel
    {
        public PartyLevel(int level, double into, double need)
        {
            Level = level;
            Into = into;
            Need = need;
        }

        public int Level { get; }
        public double Into { get; }
        public double Need { get; }
        public bool Max => Need <= 0;
    }

    public sealed class PartyGrowthRules
    {
        public static readonly PartyGrowthRules None = new PartyGrowthRules(1, 1, 0, 0, System.Array.Empty<PartyUnlock>());

        private static readonly Dictionary<string, PartyUnlockKind> Kinds = new Dictionary<string, PartyUnlockKind>
        {
            ["mascot"] = PartyUnlockKind.Mascot,
            ["colossus"] = PartyUnlockKind.Colossus,
            ["trials"] = PartyUnlockKind.Trials,
            ["tree"] = PartyUnlockKind.Tree,
            ["banner"] = PartyUnlockKind.Banner,
            ["partySets"] = PartyUnlockKind.PartySets,
            ["alliance"] = PartyUnlockKind.Alliance,
            ["bannerSlot"] = PartyUnlockKind.BannerSlot,
            ["trialTier"] = PartyUnlockKind.TrialTier,
            ["colossusTier"] = PartyUnlockKind.ColossusTier,
            ["look"] = PartyUnlockKind.Look
        };

        public PartyGrowthRules(int maxLevel, double levelBase, double levelStep, double perActiveMinute, IReadOnlyList<PartyUnlock> unlocks)
        {
            MaxLevel = maxLevel;
            LevelBase = levelBase;
            LevelStep = levelStep;
            PerActiveMinute = perActiveMinute;
            Unlocks = unlocks;
        }

        public int MaxLevel { get; }
        public double LevelBase { get; }
        public double LevelStep { get; }
        public double PerActiveMinute { get; }
        public IReadOnlyList<PartyUnlock> Unlocks { get; }

        public double Need(int level) => level >= MaxLevel ? 0 : LevelBase + LevelStep * (level - 1);

        public double TotalFor(int level)
        {
            var total = 0.0;
            for (var l = 1; l < level && l < MaxLevel; l++)
                total += Need(l);
            return total;
        }

        public PartyLevel LevelOf(double experience)
        {
            var level = 1;
            var left = System.Math.Max(0, experience);
            while (level < MaxLevel && left >= Need(level))
            {
                left -= Need(level);
                level++;
            }
            return new PartyLevel(level, level >= MaxLevel ? 0 : left, Need(level));
        }

        public bool Opened(PartyUnlockKind kind, int level)
        {
            foreach (var unlock in Unlocks)
            {
                if (unlock.Kind == kind && unlock.Level <= level)
                    return true;
            }
            return false;
        }

        public int CountOpened(PartyUnlockKind kind, int level)
        {
            var count = 0;
            foreach (var unlock in Unlocks)
            {
                if (unlock.Kind == kind && unlock.Level <= level)
                    count++;
            }
            return count;
        }

        public int NextUnlockLevel(int level)
        {
            var next = 0;
            foreach (var unlock in Unlocks)
            {
                if (unlock.Level > level && (next == 0 || unlock.Level < next))
                    next = unlock.Level;
            }
            return next;
        }

        public static PartyGrowthRules Read(ContentNode node)
        {
            var maxLevel = node.Field("maxLevel").AsInt(2, 1000);
            var unlocks = new List<PartyUnlock>();
            foreach (var item in node.Field("unlocks").Items())
            {
                var level = item.Field("level").AsInt(2, maxLevel);
                var name = item.Field("kind").AsString();
                if (!Kinds.TryGetValue(name, out var kind))
                    throw item.Field("kind").Error($"unknown unlock '{name}'");
                if (unlocks.Count > 0 && level < unlocks[unlocks.Count - 1].Level)
                    throw item.Field("level").Error("unlocks must go up by level");
                unlocks.Add(new PartyUnlock(level, kind));
            }
            var experience = node.Field("experience");
            return new PartyGrowthRules(maxLevel, node.Field("levelBase").AsNumber(1, 1e9), node.Field("levelStep").AsNumber(0, 1e9),
                experience.Field("perActiveMinute").AsNumber(0, 1000), unlocks);
        }
    }
}
