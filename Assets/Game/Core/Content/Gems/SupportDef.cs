namespace Game.Core.Content.Gems
{
    public enum SupportKind
    {
        Rebound,
        Chain,
        Executioner,
        Apprentice,
        BossHunter,
        DeathMark,
        QuietHour,
        BladeHunger,
        Acceleration,
        CritFocus,
        WideReach,
        DeferredPayment,
        DoubleCharge,
        Twins,
        Lingering,
        InkPoison,
        SeriesRhythm,
        SeriesFinish,
        LongSwing,
        Whirlpool,
        Tailwind
    }

    public sealed class SupportDef
    {
        private SupportDef(SupportKind kind)
        {
            Kind = kind;
        }

        public SupportKind Kind { get; }
        public double CooldownMultiplier { get; private set; } = 1;
        public double KillCooldownSeconds { get; private set; }
        public double QueueShare { get; private set; }
        public int QueueSlot { get; private set; }
        public double LowHpBelow { get; private set; }
        public double LowHpMultiplier { get; private set; } = 1;
        public double Power { get; private set; } = 1;
        public double ExperienceMultiplier { get; private set; } = 1;
        public double BossMultiplier { get; private set; } = 1;
        public double NormalMultiplier { get; private set; } = 1;
        public double KillResource { get; private set; }
        public double MarkedMultiplier { get; private set; } = 1;
        public double QuietSeconds { get; private set; }
        public double QuietPower { get; private set; } = 1;
        public double HungerHitsPerStep { get; private set; }
        public double HungerStep { get; private set; }
        public double HungerMax { get; private set; }
        public double FocusStep { get; private set; }
        public double FocusBonus { get; private set; }
        public double FocusBonusMax { get; private set; }
        public double CritPerFocusStep { get; private set; }
        public double CritFocusStep { get; private set; }
        public double CritMultiplier { get; private set; } = 1;
        public double CostMultiplier { get; private set; } = 1;
        public double DeferPerSecond { get; private set; }
        public int ExtraCharges { get; private set; }
        public double TwinSeconds { get; private set; }
        public double TwinPower { get; private set; }
        public double DurationMultiplier { get; private set; } = 1;
        public double LingerMax { get; private set; }
        public double PoisonPerHit { get; private set; }
        public int PoisonHitsMax { get; private set; }
        public int RhythmEvery { get; private set; }
        public double RhythmCooldownSeconds { get; private set; }
        public int RhythmLongSeries { get; private set; }
        public double RhythmPower { get; private set; } = 1;
        public int FinishAfterHits { get; private set; }
        public double FinishMultiplier { get; private set; }
        public double FinishResource { get; private set; }
        public double MouseDistanceMultiplier { get; private set; } = 1;
        public double ActiveMouseDamage { get; private set; } = 1;
        public double ActiveKeyDamage { get; private set; } = 1;
        public double CircleCooldownSeconds { get; private set; }
        public int CirclesForPower { get; private set; }
        public double CirclePower { get; private set; } = 1;
        public double WindPxPerSecond { get; private set; }
        public double WindMax { get; private set; }
        public double AuraPowerMultiplier { get; private set; } = 1;

        public static SupportDef? Read(ContentNode effect)
        {
            switch (effect.Field("type").AsString())
            {
                case "rebound":
                    return new SupportDef(SupportKind.Rebound)
                    {
                        CooldownMultiplier = effect.Field("cooldownMultiplier").AsNumber(0.1, 1),
                        KillCooldownSeconds = effect.Field("killCooldownSeconds").AsNumber(0, 600)
                    };
                case "chain":
                    return new SupportDef(SupportKind.Chain) { QueueShare = effect.Field("share").AsNumber(0, 1) };
                case "executioner":
                    return new SupportDef(SupportKind.Executioner)
                    {
                        KillCooldownSeconds = effect.Field("killCooldownSeconds").AsNumber(0, 600),
                        LowHpBelow = effect.Field("below").AsNumber(0, 1),
                        LowHpMultiplier = effect.Field("multiplier").AsNumber(1, 1000)
                    };
                case "apprentice":
                    return new SupportDef(SupportKind.Apprentice)
                    {
                        Power = effect.Field("power").AsNumber(0, 10),
                        ExperienceMultiplier = effect.Field("experienceMultiplier").AsNumber(0, 100)
                    };
                case "bossHunter":
                    return new SupportDef(SupportKind.BossHunter)
                    {
                        BossMultiplier = effect.Field("boss").AsNumber(0, 100),
                        NormalMultiplier = effect.Field("normal").AsNumber(0, 100)
                    };
                case "deathMark":
                    return new SupportDef(SupportKind.DeathMark)
                    {
                        KillResource = effect.Field("killResource").AsNumber(0, 1000),
                        MarkedMultiplier = effect.Field("markedMultiplier").AsNumber(0, 100)
                    };
                case "quietHour":
                    return new SupportDef(SupportKind.QuietHour)
                    {
                        QuietSeconds = effect.Field("idleSeconds").AsNumber(0, 86400),
                        QuietPower = effect.Field("power").AsNumber(1, 100)
                    };
                case "bladeHunger":
                    return new SupportDef(SupportKind.BladeHunger)
                    {
                        HungerHitsPerStep = effect.Field("hitsPerStep").AsNumber(1, 100000),
                        HungerStep = effect.Field("step").AsNumber(0, 10),
                        HungerMax = effect.Field("max").AsNumber(0, 100)
                    };
                case "acceleration":
                    return new SupportDef(SupportKind.Acceleration)
                    {
                        FocusStep = effect.Field("focusStep").AsNumber(0.01, 100),
                        FocusBonus = effect.Field("bonus").AsNumber(0, 10),
                        FocusBonusMax = effect.Field("max").AsNumber(0, 100)
                    };
                case "critFocus":
                    return new SupportDef(SupportKind.CritFocus)
                    {
                        CritPerFocusStep = effect.Field("critPerStep").AsNumber(0, 1),
                        CritFocusStep = effect.Field("focusStep").AsNumber(0.01, 100),
                        CritMultiplier = effect.Field("critMultiplier").AsNumber(1, 100)
                    };
                case "wideReach":
                    return new SupportDef(SupportKind.WideReach)
                    {
                        QueueShare = effect.Field("share").AsNumber(0, 1),
                        QueueSlot = effect.Field("slot").AsInt(0, Sim.GameState.QueueDepth - 1)
                    };
                case "deferredPayment":
                    return new SupportDef(SupportKind.DeferredPayment)
                    {
                        CostMultiplier = effect.Field("costMultiplier").AsNumber(0, 10),
                        DeferPerSecond = effect.Field("perSecond").AsNumber(0.001, 100)
                    };
                case "doubleCharge":
                    return new SupportDef(SupportKind.DoubleCharge)
                    {
                        ExtraCharges = effect.Field("extraCharges").AsInt(1, 10),
                        Power = effect.Field("power").AsNumber(0, 10)
                    };
                case "twins":
                    return new SupportDef(SupportKind.Twins)
                    {
                        TwinSeconds = effect.Field("afterSeconds").AsNumber(0.1, 600),
                        TwinPower = effect.Field("power").AsNumber(0, 10)
                    };
                case "lingering":
                    return new SupportDef(SupportKind.Lingering)
                    {
                        DurationMultiplier = effect.Field("durationMultiplier").AsNumber(0, 10),
                        LingerMax = effect.Field("lingerMax").AsNumber(0, 10)
                    };
                case "inkPoison":
                    return new SupportDef(SupportKind.InkPoison)
                    {
                        PoisonPerHit = effect.Field("stacksPerHit").AsNumber(0, 1000),
                        PoisonHitsMax = effect.Field("maxHits").AsInt(1, 1000)
                    };
                case "seriesRhythm":
                    return new SupportDef(SupportKind.SeriesRhythm)
                    {
                        RhythmEvery = effect.Field("every").AsInt(1, 100000),
                        RhythmCooldownSeconds = effect.Field("cooldownSeconds").AsNumber(0, 600),
                        RhythmLongSeries = effect.Field("longSeries").AsInt(1, 100000),
                        RhythmPower = effect.Field("power").AsNumber(1, 100)
                    };
                case "seriesFinish":
                    return new SupportDef(SupportKind.SeriesFinish)
                    {
                        FinishAfterHits = effect.Field("afterHits").AsInt(1, 100000),
                        FinishMultiplier = effect.Field("multiplier").AsNumber(0, 1000),
                        FinishResource = effect.Field("resource").AsNumber(0, 1000)
                    };
                case "longSwing":
                    return new SupportDef(SupportKind.LongSwing)
                    {
                        MouseDistanceMultiplier = effect.Field("distanceMultiplier").AsNumber(0, 100),
                        ActiveMouseDamage = effect.Field("mouseDamage").AsNumber(0, 100)
                    };
                case "whirlpool":
                    return new SupportDef(SupportKind.Whirlpool)
                    {
                        CircleCooldownSeconds = effect.Field("cooldownSecondsPerCircle").AsNumber(0, 600),
                        CirclesForPower = effect.Field("circlesForPower").AsInt(1, 1000),
                        CirclePower = effect.Field("power").AsNumber(1, 100)
                    };
                case "tailwind":
                    return new SupportDef(SupportKind.Tailwind)
                    {
                        WindPxPerSecond = effect.Field("pxPerSecond").AsNumber(1, 1e7),
                        WindMax = effect.Field("max").AsNumber(0, 10),
                        AuraPowerMultiplier = effect.Field("auraPower").AsNumber(0, 100)
                    };
                default:
                    return null;
            }
        }
    }
}
