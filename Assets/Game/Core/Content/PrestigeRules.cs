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

        public BigNumber EchoFor(BigNumber runGold)
        {
            if (runGold.Sign <= 0)
                return BigNumber.Zero;
            var echo = (runGold / GoldBase).Pow(Power) * K;
            return echo.Log10() < 15 ? BigNumber.FromDouble(System.Math.Floor(echo.ToDouble())) : echo;
        }

        public static PrestigeRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var echo = root.Field("echo");
            return new PrestigeRules(
                root.Field("minStage").AsLong(1),
                echo.Field("k").AsNumber(1e-9, 1e12),
                echo.Field("goldBase").AsNumber(1e-9, 1e300),
                echo.Field("power").AsNumber(0.01, 1),
                root.Field("highlightGain").AsNumber(0, 100));
        }
    }
}
