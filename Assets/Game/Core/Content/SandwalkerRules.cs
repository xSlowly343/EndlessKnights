namespace Game.Core.Content
{
    public sealed class SandwalkerRules
    {
        public static readonly SandwalkerRules None = new SandwalkerRules();

        public BlindRules Blind { get; private set; } = BlindRules.None;
        public double PourPx { get; private set; } = 30;
        public double StepRecentSeconds { get; private set; } = 1;
        public double StepHitMax { get; private set; } = 10;
        public double PartyTimeSlow { get; private set; } = 0.5;
        public int FlipEvery { get; private set; } = 200;
        public int FlipEveryMin { get; private set; } = 40;
        public double FlipPenalty { get; private set; } = 0.5;
        public double FlipStepPx { get; private set; } = 200;
        public double FlipPartySlow { get; private set; } = 1;
        public double CampPx { get; private set; } = 3000;
        public double CampPxMin { get; private set; } = 500;
        public double CampSand { get; private set; } = 10;
        public double CampCutSeconds { get; private set; } = 1;
        public double CaravanIdlePx { get; private set; } = 100;
        public double CaravanPartyCooldown { get; private set; } = 0.1;
        public double CaravanPartySeconds { get; private set; } = 5;
        public int OasisEvery { get; private set; } = 5;
        public double OasisMultiplier { get; private set; } = 3;
        public double OasisSandMultiplier { get; private set; } = 2.5;
        public double CaravanPartyGold { get; private set; } = 0.25;
        public double CaravanGoldSeconds { get; private set; } = 10;
        public double MiragePx { get; private set; } = 2000;
        public double MiragePxMin { get; private set; } = 500;
        public int MirageMax { get; private set; } = 5;
        public double MirageSeconds { get; private set; } = 20;
        public double MirageSecondsMin { get; private set; } = 2;
        public double MirageStrikeSeconds { get; private set; } = 1;
        public double MirageTime { get; private set; } = 2;
        public double MirageSelfPenalty { get; private set; } = 0.4;
        public double MiragePartySlow { get; private set; } = 0.06;
        public double MirageBlindSeconds { get; private set; } = 1;

        public static SandwalkerRules Read(ContentNode node)
        {
            var common = node.Field("common");
            var flip = node.Field("flip");
            var caravan = node.Field("caravan");
            var mirage = node.Field("mirage");
            return new SandwalkerRules
            {
                Blind = BlindRules.Read(node.Field("blind")),
                PourPx = common.Field("pourPx").AsNumber(0, 1e6),
                StepRecentSeconds = common.Field("stepRecentSeconds").AsNumber(0.1, 600),
                StepHitMax = common.Field("stepHitMax").AsNumber(0, 1000),
                PartyTimeSlow = common.Field("partyTimeSlow").AsNumber(0.01, 1),
                FlipEvery = flip.Field("every").AsInt(1, 100_000),
                FlipEveryMin = flip.Field("everyMin").AsInt(1, 100_000),
                FlipPenalty = flip.Field("penalty").AsNumber(0, 0.95),
                FlipStepPx = flip.Field("stepPx").AsNumber(1, 1e6),
                FlipPartySlow = flip.Field("partySlow").AsNumber(0, 1),
                CampPx = caravan.Field("campPx").AsNumber(1, 1e7),
                CampPxMin = caravan.Field("campPxMin").AsNumber(1, 1e7),
                CampSand = caravan.Field("sand").AsNumber(0, 100),
                CampCutSeconds = caravan.Field("cutSeconds").AsNumber(0, 600),
                CaravanIdlePx = caravan.Field("idlePx").AsNumber(0, 1e6),
                CaravanPartyCooldown = caravan.Field("partyCooldown").AsNumber(0, 0.9),
                CaravanPartySeconds = caravan.Field("partySeconds").AsNumber(0, 600),
                OasisEvery = caravan.Field("oasisEvery").AsInt(1, 1000),
                OasisMultiplier = caravan.Field("oasisMultiplier").AsNumber(1, 100),
                OasisSandMultiplier = caravan.Field("oasisSandMultiplier").AsNumber(1, 100),
                CaravanPartyGold = caravan.Field("partyGold").AsNumber(0, 10),
                CaravanGoldSeconds = caravan.Field("goldSeconds").AsNumber(0, 600),
                MiragePx = mirage.Field("px").AsNumber(1, 1e7),
                MiragePxMin = mirage.Field("pxMin").AsNumber(1, 1e7),
                MirageMax = mirage.Field("max").AsInt(1, Sim.SandwalkerState.MirageCapacity),
                MirageSeconds = mirage.Field("seconds").AsNumber(0.1, 3600),
                MirageSecondsMin = mirage.Field("secondsMin").AsNumber(0.1, 3600),
                MirageStrikeSeconds = mirage.Field("strikeSeconds").AsNumber(0.1, 600),
                MirageTime = mirage.Field("timeSeconds").AsNumber(0, 600),
                MirageSelfPenalty = mirage.Field("selfPenalty").AsNumber(0, 0.95),
                MiragePartySlow = mirage.Field("partySlow").AsNumber(0, 1),
                MirageBlindSeconds = mirage.Field("blindSeconds").AsNumber(0, 600)
            };
        }
    }
}
