using Game.Core.Content.Gems;

namespace Game.Core.Content
{
    public sealed class MinionRules
    {
        public MinionRules(int max, double mouseSpeed, int riseEvery = 0, int riseEveryMin = 0, int riseMax = 0, SummonEffect? rise = null)
        {
            Max = max;
            MouseSpeed = mouseSpeed;
            RiseEvery = riseEvery;
            RiseEveryMin = riseEveryMin;
            RiseMax = riseMax;
            Rise = rise;
        }

        public int Max { get; }
        public double MouseSpeed { get; }
        public int RiseEvery { get; }
        public int RiseEveryMin { get; }
        public int RiseMax { get; }
        public SummonEffect? Rise { get; }

        public static MinionRules Read(ContentNode node)
        {
            var max = node.Field("max").AsInt(0, Sim.MinionSystem.Capacity);
            var mouseSpeed = node.Field("mouseSpeed").AsNumber(1, 100);
            if (!node.HasField("rise"))
                return new MinionRules(max, mouseSpeed);
            var rise = node.Field("rise");
            return new MinionRules(max, mouseSpeed,
                rise.Field("every").AsInt(1, 1000),
                rise.Field("everyMin").AsInt(1, 1000),
                rise.Field("max").AsInt(1, Sim.MinionSystem.Capacity),
                new SummonEffect(MinionKind.Skeleton, rise.Field("seconds").AsNumber(1, 3600), rise.Field("hitsPerSecond").AsNumber(0.1, 100), rise.Field("multiplier").AsNumber(0, 1000)));
        }
    }
}
