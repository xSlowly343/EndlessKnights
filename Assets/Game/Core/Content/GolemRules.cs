using Game.Core.Content.Gems;

namespace Game.Core.Content
{
    public sealed class GolemRules
    {
        public static readonly GolemRules None = new GolemRules(new SummonEffect(MinionKind.Golem, 1, 0.1, 0), 0);

        public GolemRules(SummonEffect summon, double stunSeconds)
        {
            Summon = summon;
            StunSeconds = stunSeconds;
        }

        public SummonEffect Summon { get; }
        public double StunSeconds { get; }

        public static GolemRules Read(ContentNode node) =>
            new GolemRules(new SummonEffect(MinionKind.Golem, node.Field("seconds").AsNumber(1, 3600), node.Field("hitsPerSecond").AsNumber(0.01, 100), node.Field("multiplier").AsNumber(0, 1e6)),
                node.Field("stunSeconds").AsNumber(0, 60));
    }
}
