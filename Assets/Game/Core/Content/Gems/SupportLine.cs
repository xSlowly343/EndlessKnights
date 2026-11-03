using System;
using System.Collections.Generic;

namespace Game.Core.Content.Gems
{
    public enum SupportLineEffect
    {
        Power,
        CritChance,
        CritMultiplier,
        Experience,
        KillReward
    }

    public enum SupportSignal
    {
        None,
        Boss,
        PartyMembers,
        IdleSeconds,
        HitsSinceCast,
        Focus
    }

    public readonly struct SupportSignals
    {
        public readonly double Boss;
        public readonly double PartyMembers;
        public readonly double IdleSeconds;
        public readonly double HitsSinceCast;
        public readonly double Focus;

        public SupportSignals(double boss, double partyMembers, double idleSeconds, double hitsSinceCast, double focus)
        {
            Boss = boss;
            PartyMembers = partyMembers;
            IdleSeconds = idleSeconds;
            HitsSinceCast = hitsSinceCast;
            Focus = focus;
        }

        public double Of(SupportSignal signal) => signal switch
        {
            SupportSignal.Boss => Boss,
            SupportSignal.PartyMembers => PartyMembers,
            SupportSignal.IdleSeconds => IdleSeconds,
            SupportSignal.HitsSinceCast => HitsSinceCast,
            SupportSignal.Focus => Focus,
            _ => 0
        };
    }

    public readonly struct SupportLine
    {
        private static readonly Dictionary<string, SupportLineEffect> Effects = new Dictionary<string, SupportLineEffect>(StringComparer.Ordinal)
        {
            ["power"] = SupportLineEffect.Power,
            ["critChance"] = SupportLineEffect.CritChance,
            ["critMultiplier"] = SupportLineEffect.CritMultiplier,
            ["experience"] = SupportLineEffect.Experience,
            ["killReward"] = SupportLineEffect.KillReward
        };

        private static readonly Dictionary<string, SupportSignal> Signals = new Dictionary<string, SupportSignal>(StringComparer.Ordinal)
        {
            ["boss"] = SupportSignal.Boss,
            ["partyMembers"] = SupportSignal.PartyMembers,
            ["idleSeconds"] = SupportSignal.IdleSeconds,
            ["hitsSinceCast"] = SupportSignal.HitsSinceCast,
            ["focus"] = SupportSignal.Focus
        };

        public readonly SupportLineEffect Effect;
        public readonly double Value;
        public readonly SupportSignal WhenSignal;
        public readonly double WhenAtLeast;
        public readonly bool Unless;
        public readonly SupportSignal PerSignal;
        public readonly double PerStep;
        public readonly int PerCap;

        private SupportLine(SupportLineEffect effect, double value, SupportSignal whenSignal, double whenAtLeast, bool unless, SupportSignal perSignal, double perStep, int perCap)
        {
            Effect = effect;
            Value = value;
            WhenSignal = whenSignal;
            WhenAtLeast = whenAtLeast;
            Unless = unless;
            PerSignal = perSignal;
            PerStep = perStep;
            PerCap = perCap;
        }

        public double Amount(in SupportSignals signals)
        {
            if (WhenSignal != SupportSignal.None && (signals.Of(WhenSignal) >= WhenAtLeast) == Unless)
                return 0;
            if (PerSignal == SupportSignal.None)
                return Value;
            var steps = System.Math.Min(PerCap, System.Math.Floor(System.Math.Max(0, signals.Of(PerSignal)) / PerStep + 1e-9));
            return Value * steps;
        }

        public static SupportLine Read(ContentNode node)
        {
            var effectNode = node.Field("effect");
            if (!Effects.TryGetValue(effectNode.AsString(), out var effect))
                throw effectNode.Error($"unknown support line effect '{effectNode.AsString()}', expected one of {string.Join(", ", Effects.Keys)}");
            var value = node.Field("value").AsNumber(-0.99, 100);
            if (node.HasField("when") && node.HasField("unless"))
                throw node.Error("a line takes 'when' or 'unless', not both");
            var whenSignal = SupportSignal.None;
            var atLeast = 1.0;
            var unless = node.HasField("unless");
            if (node.HasField("when") || unless)
            {
                var when = node.Field(unless ? "unless" : "when");
                whenSignal = ReadSignal(when.Field("signal"));
                atLeast = when.HasField("atLeast") ? when.Field("atLeast").AsNumber(0, 1e9) : 1;
            }
            var perSignal = SupportSignal.None;
            var step = 0.0;
            var cap = 0;
            if (node.HasField("per"))
            {
                var per = node.Field("per");
                perSignal = ReadSignal(per.Field("signal"));
                step = per.Field("step").AsNumber(1e-6, 1e9);
                cap = per.Field("cap").AsInt(1, 100000);
            }
            return new SupportLine(effect, value, whenSignal, atLeast, unless, perSignal, step, cap);
        }

        private static SupportSignal ReadSignal(ContentNode node)
        {
            if (!Signals.TryGetValue(node.AsString(), out var signal))
                throw node.Error($"unknown support signal '{node.AsString()}', expected one of {string.Join(", ", Signals.Keys)}");
            return signal;
        }
    }
}
