using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class SoulbinderState
    {
        public const int Capacity = 16;

        public double[] Strength { get; } = new double[Capacity];
        public BigNumber[] SourceHp { get; } = new BigNumber[Capacity];
        public int Souls { get; set; }
        public long SoulsTaken { get; set; }
        public double SpiritCarry { get; set; }
        public int ReleaseTicks { get; set; }
        public long Breaths { get; set; }
        public int ColdTicks { get; set; }
        public double ColdHits { get; set; }
        public int ColdResendTicks { get; set; }
        public int LendHits { get; set; }
        public long Lends { get; set; }
        public long SoulsBack { get; set; }
    }
}
