using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public abstract class SkillEffect
    {
    }

    public sealed class ChainHitsEffect : SkillEffect
    {
        public ChainHitsEffect(int hits, double nextEnemyShare)
        {
            Hits = hits;
            NextEnemyShare = nextEnemyShare;
        }

        public int Hits { get; }
        public double NextEnemyShare { get; }
    }

    public sealed class StrikeEffect : SkillEffect
    {
        public StrikeEffect(double multiplier, int queueTargets = 0, bool skipCurrent = false, double killRefundShare = 0)
        {
            Multiplier = multiplier;
            QueueTargets = queueTargets;
            SkipCurrent = skipCurrent;
            KillRefundShare = killRefundShare;
        }

        public double Multiplier { get; }
        public int QueueTargets { get; }
        public bool SkipCurrent { get; }
        public double KillRefundShare { get; }
    }

    public sealed class AuraEffect : SkillEffect
    {
        public AuraEffect(IReadOnlyList<Modifier> modifiers, bool party = false, double reserve = 0, double focusLockAfterSeconds = 0)
        {
            Modifiers = modifiers;
            Party = party;
            Reserve = reserve;
            FocusLockAfterSeconds = focusLockAfterSeconds;
        }

        public double FocusLockAfterSeconds { get; }

        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool Party { get; }
        public double Reserve { get; }
        public bool Reserves => Reserve > 0;
    }

    public sealed class StoreHitsEffect : SkillEffect
    {
        public StoreHitsEffect(double releaseShare)
        {
            ReleaseShare = releaseShare;
        }

        public double ReleaseShare { get; }
    }

    public sealed class QueueHitsEffect : SkillEffect
    {
        public QueueHitsEffect(double share)
        {
            Share = share;
        }

        public double Share { get; }
    }

    public sealed class KeyRepeatEffect : SkillEffect
    {
        public KeyRepeatEffect(int every, double multiplier)
        {
            Every = every;
            Multiplier = multiplier;
        }

        public int Every { get; }
        public double Multiplier { get; }
    }

    public enum SpecialKey
    {
        Space,
        Backspace,
        Combo
    }

    public sealed class SpecialKeysEffect : SkillEffect
    {
        public SpecialKeysEffect(SpecialKey key, double bonus, int charges, bool passive)
        {
            Key = key;
            Bonus = bonus;
            Charges = charges;
            Passive = passive;
        }

        public SpecialKey Key { get; }
        public double Bonus { get; }
        public int Charges { get; }
        public bool Passive { get; }
    }

    public sealed class StreakStrikeEffect : SkillEffect
    {
        public StreakStrikeEffect(int hits, double maxGapSeconds, double multiplier)
        {
            Hits = hits;
            MaxGapSeconds = maxGapSeconds;
            Multiplier = multiplier;
        }

        public int Hits { get; }
        public double MaxGapSeconds { get; }
        public double Multiplier { get; }
    }

    public sealed class FlowEffect : SkillEffect
    {
        public FlowEffect(double afterSeconds, double maxGapSeconds, IReadOnlyList<Modifier> modifiers)
        {
            AfterSeconds = afterSeconds;
            MaxGapSeconds = maxGapSeconds;
            Modifiers = modifiers;
        }

        public double AfterSeconds { get; }
        public double MaxGapSeconds { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public enum StackTrigger
    {
        Kill,
        KeyHit
    }

    public sealed class StacksEffect : SkillEffect
    {
        public StacksEffect(StackTrigger trigger, Modifier perStack, double maxTotal, double stackSeconds)
        {
            Trigger = trigger;
            PerStack = perStack;
            MaxTotal = maxTotal;
            StackSeconds = stackSeconds;
        }

        public StackTrigger Trigger { get; }
        public Modifier PerStack { get; }
        public double MaxTotal { get; }
        public double StackSeconds { get; }
        public bool Passive => Trigger == StackTrigger.Kill;

        public Modifier For(int stacks) =>
            new Modifier(PerStack.Stat, PerStack.Kind, System.Math.Min(MaxTotal, PerStack.Value * stacks), PerStack.Tags);
    }

    public sealed class EnemyDebuffEffect : SkillEffect
    {
        public EnemyDebuffEffect(double damageTaken)
        {
            DamageTaken = damageTaken;
        }

        public double DamageTaken { get; }
    }

    public sealed class IdleStrikeEffect : SkillEffect
    {
        public IdleStrikeEffect(double chargePerSecond, double maxChargeSeconds, double minIdleSeconds)
        {
            ChargePerSecond = chargePerSecond;
            MaxChargeSeconds = maxChargeSeconds;
            MinIdleSeconds = minIdleSeconds;
        }

        public double ChargePerSecond { get; }
        public double MaxChargeSeconds { get; }
        public double MinIdleSeconds { get; }
    }
}
