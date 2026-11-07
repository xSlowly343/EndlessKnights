using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class DefenseRules
    {
        private long[] _stages = System.Array.Empty<long>();
        private double[] _shares = System.Array.Empty<double>();

        public double K { get; private set; } = 1;
        public double MaxShare { get; private set; }
        public double BossExtra { get; private set; }
        public double ArmorPenCap { get; private set; }
        public double ShredCap { get; private set; }

        public double Share(long stage, bool boss)
        {
            if (_stages.Length == 0)
                return 0;
            var share = Curve(stage);
            if (boss && share > 0)
                share += BossExtra;
            return System.Math.Min(MaxShare + (boss ? BossExtra : 0), share);
        }

        public double Defense(long stage, bool boss) => Share(stage, boss) * K;

        public double Cut(double defense) => defense > 0 ? defense / (defense + K) : 0;

        public double Gain(double defense, double shred, double pen)
        {
            if (!(defense > 0))
                return 1;
            var after = defense * (1 - System.Math.Min(ShredCap, System.Math.Max(0, shred))) * (1 - System.Math.Min(ArmorPenCap, System.Math.Max(0, pen)));
            return (defense + K) / (after + K);
        }

        private double Curve(long stage)
        {
            if (stage <= _stages[0])
                return _shares[0];
            for (var i = 1; i < _stages.Length; i++)
            {
                if (stage <= _stages[i])
                    return Lerp(i - 1, i, stage);
            }
            var last = _stages.Length - 1;
            return last == 0 ? _shares[0] : Lerp(last - 1, last, stage);
        }

        private double Lerp(int from, int to, long stage) =>
            _shares[from] + (_shares[to] - _shares[from]) * (stage - _stages[from]) / (double)(_stages[to] - _stages[from]);

        internal static DefenseRules Read(ContentNode node)
        {
            var stages = new List<long>();
            var shares = new List<double>();
            foreach (var point in node.Field("curve").Items())
            {
                var stage = point.Field("stage").AsLong(1);
                if (stages.Count > 0 && stage <= stages[stages.Count - 1])
                    throw point.Field("stage").Error("stages of the defense curve go up");
                stages.Add(stage);
                shares.Add(point.Field("share").AsNumber(0, 100));
            }
            return new DefenseRules
            {
                K = node.Field("k").AsNumber(1, 1e12),
                MaxShare = node.Field("maxShare").AsNumber(0, 100),
                BossExtra = node.Field("bossExtra").AsNumber(0, 100),
                ArmorPenCap = node.Field("armorPenCap").AsNumber(0, 1),
                ShredCap = node.Field("shredCap").AsNumber(0, 1),
                _stages = stages.ToArray(),
                _shares = shares.ToArray()
            };
        }
    }
}
