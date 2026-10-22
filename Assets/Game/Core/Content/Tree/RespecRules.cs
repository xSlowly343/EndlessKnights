using Game.Core.Math;

namespace Game.Core.Content.Tree
{
    public sealed class RespecRules
    {
        public RespecRules(ExponentialCurve cost, double levelRate, double attributeShare, double freeFullResetHours)
        {
            Cost = cost;
            LevelRate = levelRate;
            AttributeShare = attributeShare;
            FreeFullResetHours = freeFullResetHours;
        }

        public ExponentialCurve Cost { get; }
        public double LevelRate { get; }
        public double AttributeShare { get; }
        public double FreeFullResetHours { get; }

        public BigNumber NodeCost(int depth, int heroLevel, bool attribute) =>
            Cost.At(System.Math.Max(1, depth)) * (System.Math.Pow(LevelRate, System.Math.Max(0, heroLevel - 1)) * (attribute ? AttributeShare : 1));

        public static RespecRules Read(ContentNode node) =>
            new RespecRules(
                ExponentialCurve.Read(node.Field("cost")),
                node.Field("levelRate").AsNumber(1, 10),
                node.Field("attributeShare").AsNumber(0, 1),
                node.Field("freeFullResetHours").AsNumber(0, 24 * 365));
    }
}
