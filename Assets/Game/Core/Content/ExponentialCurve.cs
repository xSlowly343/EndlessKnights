using System;
using Game.Core.Math;

namespace Game.Core.Content
{
    public readonly struct ExponentialCurve
    {
        public readonly double Base;
        public readonly double Rate;

        public ExponentialCurve(double @base, double rate)
        {
            Base = @base;
            Rate = rate;
        }

        public BigNumber At(long level) => FromLog10(System.Math.Log10(Base) + (level - 1) * System.Math.Log10(Rate));

        public static BigNumber FromLog10(double log10)
        {
            var whole = System.Math.Floor(log10);
            return BigNumber.Create(System.Math.Pow(10d, log10 - whole), (long)whole);
        }

        public static ExponentialCurve Read(ContentNode node)
        {
            var kind = node.Field("curve").AsString();
            if (!string.Equals(kind, "exp", StringComparison.Ordinal))
                throw node.Field("curve").Error($"unsupported curve '{kind}', expected 'exp'");
            return new ExponentialCurve(
                node.Field("base").AsNumber(double.Epsilon, double.MaxValue),
                node.Field("rate").AsNumber(1.0, 100.0));
        }
    }
}
