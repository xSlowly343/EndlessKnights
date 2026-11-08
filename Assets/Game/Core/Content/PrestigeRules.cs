using Game.Core.Math;

namespace Game.Core.Content
{
    public sealed class PrestigeRules
    {
        public const string FileName = "prestige.json";
        public const int SupportedVersion = 1;

        private PrestigeRules(long minStage, double k, double goldBase, double power, double highlightGain)
        {
            MinStage = minStage;
            K = k;
            GoldBase = goldBase;
            Power = power;
            HighlightGain = highlightGain;
        }

        public long MinStage { get; }
        public double K { get; }
        public double GoldBase { get; }
        public double Power { get; }
        public double HighlightGain { get; }
        public double MaxStartShare { get; private set; } = 0.9;
        public bool ByStage => _stageSteps.Length > 0;
        private double _stageBase;
        private (long From, double Log10Rate)[] _stageSteps = System.Array.Empty<(long, double)>();
        public double RushRecordShare { get; private set; } = 0.99;

        public BigNumber EchoFor(BigNumber runGold)
        {
            if (runGold.Sign <= 0)
                return BigNumber.Zero;
            var echo = (runGold / GoldBase).Pow(Power) * K;
            return echo.Log10() < 15 ? BigNumber.FromDouble(System.Math.Floor(echo.ToDouble())) : echo;
        }

        public BigNumber EchoForStage(long stage)
        {
            if (stage < MinStage)
                return BigNumber.Zero;
            var log10 = System.Math.Log10(_stageBase);
            for (var i = 0; i < _stageSteps.Length; i++)
            {
                var from = System.Math.Max(MinStage, _stageSteps[i].From);
                var to = i + 1 < _stageSteps.Length ? _stageSteps[i + 1].From : long.MaxValue;
                if (stage <= from)
                    break;
                log10 += (System.Math.Min(stage, to) - from) * _stageSteps[i].Log10Rate;
            }
            var whole = System.Math.Floor(log10);
            var echo = BigNumber.Create(System.Math.Pow(10d, log10 - whole), (long)whole);
            return log10 < 15 ? BigNumber.FromDouble(System.Math.Floor(echo.ToDouble())) : echo;
        }

        private static (long, double)[] ReadStageSteps(ContentNode node)
        {
            var steps = new System.Collections.Generic.List<(long, double)> { (1, System.Math.Log10(node.Field("rate").AsNumber(1.0, 10.0))) };
            if (!node.HasField("steps"))
                return steps.ToArray();
            foreach (var item in node.Field("steps").Items())
            {
                var from = item.Field("from").AsLong(2);
                if (from <= steps[steps.Count - 1].Item1)
                    throw item.Field("from").Error("steps must go up by stage");
                steps.Add((from, System.Math.Log10(item.Field("rate").AsNumber(1.0, 10.0))));
            }
            return steps.ToArray();
        }

        public static PrestigeRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var echo = root.Field("echo");
            var rules = new PrestigeRules(
                root.Field("minStage").AsLong(1),
                echo.Field("k").AsNumber(1e-9, 1e12),
                echo.Field("goldBase").AsNumber(1e-9, 1e300),
                echo.Field("power").AsNumber(0.01, 1),
                root.Field("highlightGain").AsNumber(0, 100))
            {
                MaxStartShare = root.HasField("maxStartShare") ? root.Field("maxStartShare").AsNumber(0, 1) : 0.9,
                RushRecordShare = root.HasField("rushRecordShare") ? root.Field("rushRecordShare").AsNumber(0, 1) : 0.99
            };
            if (echo.HasField("byStage"))
            {
                var byStage = echo.Field("byStage");
                rules._stageBase = byStage.Field("base").AsNumber(1e-9, 1e12);
                rules._stageSteps = ReadStageSteps(byStage);
            }
            return rules;
        }
    }
}
