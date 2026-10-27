using Game.Core.Content.Gems;

namespace Game.Core.Content
{
    public sealed class PathRules
    {
        public PathRules(double balanceWindowSeconds, double doomSeconds, SummonEffect skeletonOnKill, SummonEffect beastOnKill)
        {
            BalanceWindowSeconds = balanceWindowSeconds;
            DoomSeconds = doomSeconds;
            SkeletonOnKill = skeletonOnKill;
            BeastOnKill = beastOnKill;
        }

        public double BalanceWindowSeconds { get; }
        public double DoomSeconds { get; }
        public SummonEffect SkeletonOnKill { get; }
        public SummonEffect BeastOnKill { get; }

        public static PathRules Read(ContentNode node)
        {
            var onKill = node.Field("onKill");
            return new PathRules(
                node.Field("balanceWindowSeconds").AsNumber(0.1, 3600),
                node.Field("doom").Field("seconds").AsNumber(0.1, 600),
                ReadSummon(onKill.Field("skeleton"), MinionKind.Skeleton),
                ReadSummon(onKill.Field("beast"), MinionKind.Beast));
        }

        private static SummonEffect ReadSummon(ContentNode node, MinionKind kind) =>
            new SummonEffect(kind,
                node.Field("seconds").AsNumber(0.1, 3600),
                node.Field("hitsPerSecond").AsNumber(0, 100),
                node.Field("multiplier").AsNumber(0, 1000));
    }
}
