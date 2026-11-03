namespace Game.Core.Content.Gems
{
    public enum SupportKind
    {
        Rebound,
        Chain,
        Executioner,
        DeathMark,
        WideReach,
        DeferredPayment,
        DoubleCharge,
        Twins,
        Lingering,
        InkPoison,
        SeriesRhythm,
        SeriesFinish,
        LongSwing,
        Tailwind,
        MinionTrail,
        TargetHunter,
        Cursed,
        LingeringBlight,
        KeyEcho,
        Lines
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
        public double KillResource { get; private set; }
        public double MarkedMultiplier { get; private set; } = 1;
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
        public double WindPxPerSecond { get; private set; }
        public double WindMax { get; private set; }
        public double AuraPowerMultiplier { get; private set; } = 1;
        public double MinionMouseSpeed { get; private set; } = 1;
        public double MinionLife { get; private set; } = 1;
        public double RedirectMultiplier { get; private set; }
        public double VulnerabilityShare { get; private set; }
        public double VulnerabilitySeconds { get; private set; }
        public double AfflictionMultiplier { get; private set; } = 1;
        public double StackCapMultiplier { get; private set; } = 1;
        public int EchoEveryKeys { get; private set; }
        public double EchoPower { get; private set; }

        public System.Collections.Generic.IReadOnlyList<SupportLine> Lines { get; private set; } = System.Array.Empty<SupportLine>();
        public System.Collections.Generic.IReadOnlyList<Stats.Modifier> PartyLines { get; private set; } = System.Array.Empty<Stats.Modifier>();

        private static SupportDef ReadLines(ContentNode effect)
        {
            var lines = new System.Collections.Generic.List<SupportLine>();
            if (effect.HasField("lines"))
            {
                foreach (var item in effect.Field("lines").Items())
                    lines.Add(SupportLine.Read(item));
            }
            var party = effect.HasField("party") ? ModifierReader.ReadList(effect.Field("party")) : System.Array.Empty<Stats.Modifier>();
            foreach (var modifier in party)
            {
                if (!Party.CoopLine.IsCoop(modifier.Stat))
                    throw effect.Field("party").Error("a party line needs 'to' (self, party, allies or enemy)");
            }
            if (lines.Count == 0 && party.Count == 0)
                throw effect.Error("a lines support needs 'lines' or 'party'");
            return new SupportDef(SupportKind.Lines) { Lines = lines, PartyLines = party };
        }

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
                case "deathMark":
                    return new SupportDef(SupportKind.DeathMark)
                    {
                        KillResource = effect.Field("killResource").AsNumber(0, 1000),
                        MarkedMultiplier = effect.Field("markedMultiplier").AsNumber(0, 100)
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
                case "tailwind":
                    return new SupportDef(SupportKind.Tailwind)
                    {
                        WindPxPerSecond = effect.Field("pxPerSecond").AsNumber(1, 1e7),
                        WindMax = effect.Field("max").AsNumber(0, 10),
                        AuraPowerMultiplier = effect.Field("auraPower").AsNumber(0, 100)
                    };
                case "minionTrail":
                    return new SupportDef(SupportKind.MinionTrail)
                    {
                        MinionMouseSpeed = effect.Field("mouseSpeed").AsNumber(0, 100),
                        MinionLife = effect.Field("life").AsNumber(0, 100)
                    };
                case "targetHunter":
                    return new SupportDef(SupportKind.TargetHunter) { RedirectMultiplier = effect.Field("multiplier").AsNumber(0, 100) };
                case "cursed":
                    return new SupportDef(SupportKind.Cursed)
                    {
                        VulnerabilityShare = effect.Field("damageTaken").AsNumber(0, 10),
                        VulnerabilitySeconds = effect.Field("seconds").AsNumber(0.1, 600)
                    };
                case "lingeringBlight":
                    return new SupportDef(SupportKind.LingeringBlight)
                    {
                        AfflictionMultiplier = effect.Field("afflictionMultiplier").AsNumber(0, 100),
                        StackCapMultiplier = effect.Field("stackCapMultiplier").AsNumber(0, 100)
                    };
                case "keyEcho":
                    return new SupportDef(SupportKind.KeyEcho)
                    {
                        EchoEveryKeys = effect.Field("everyKeys").AsInt(1, 100000),
                        EchoPower = effect.Field("power").AsNumber(0, 10),
                        ActiveKeyDamage = effect.Field("keyDamage").AsNumber(0, 100)
                    };
                case "lines":
                    return ReadLines(effect);
                default:
                    return null;
            }
        }
    }
}
