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
        public StrikeEffect(double multiplier, int queueTargets = 0, bool skipCurrent = false, double killRefundShare = 0,
            double queueMultiplier = 0, double executeBelow = 0, bool coinFlip = false, double critChance = 0, bool arrow = false)
        {
            CritChance = critChance;
            Arrow = arrow;
            QueueMultiplier = queueMultiplier > 0 ? queueMultiplier : multiplier;
            ExecuteBelow = executeBelow;
            CoinFlip = coinFlip;
            Multiplier = multiplier;
            QueueTargets = queueTargets;
            SkipCurrent = skipCurrent;
            KillRefundShare = killRefundShare;
        }

        public double Multiplier { get; }
        public int QueueTargets { get; }
        public bool SkipCurrent { get; }
        public double KillRefundShare { get; }
        public double QueueMultiplier { get; }
        public double ExecuteBelow { get; }
        public bool CoinFlip { get; }
        public double CritChance { get; }
        public bool Arrow { get; }
    }

    public sealed class AuraEffect : SkillEffect
    {
        public AuraEffect(IReadOnlyList<Modifier> modifiers, bool party = false, double reserve = 0, double focusLockAfterSeconds = 0,
            double keylessStrikeSeconds = 0, double keylessStrikeMultiplier = 0)
        {
            KeylessStrikeSeconds = keylessStrikeSeconds;
            KeylessStrikeMultiplier = keylessStrikeMultiplier;
            Modifiers = modifiers;
            Party = party;
            Reserve = reserve;
            FocusLockAfterSeconds = focusLockAfterSeconds;
        }

        public double FocusLockAfterSeconds { get; }
        public double KeylessStrikeSeconds { get; }
        public double KeylessStrikeMultiplier { get; }

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

    public enum ArmOn
    {
        None,
        Flick,
        Circle
    }

    public sealed class InputHitsEffect : SkillEffect
    {
        public InputHitsEffect(double pxPerHit, double pxShare, double circleMultiplier, double flickMultiplier, double reversalMultiplier, int maxHits)
        {
            PxPerHit = pxPerHit;
            PxShare = pxShare;
            CircleMultiplier = circleMultiplier;
            FlickMultiplier = flickMultiplier;
            ReversalMultiplier = reversalMultiplier;
            MaxHits = maxHits;
        }

        public double PxPerHit { get; }
        public double PxShare { get; }
        public double CircleMultiplier { get; }
        public double FlickMultiplier { get; }
        public double ReversalMultiplier { get; }
        public int MaxHits { get; }
    }

    public sealed class DistanceStacksEffect : SkillEffect
    {
        public DistanceStacksEffect(double pxPerStack, int windowSeconds, StacksEffect stacks)
        {
            PxPerStack = pxPerStack;
            WindowSeconds = windowSeconds;
            Stacks = stacks;
        }

        public double PxPerStack { get; }
        public int WindowSeconds { get; }
        public StacksEffect Stacks { get; }
    }

    public sealed class TrailEffect : SkillEffect
    {
        public TrailEffect(double pxPerMultiplier)
        {
            PxPerMultiplier = pxPerMultiplier;
        }

        public double PxPerMultiplier { get; }
    }

    public sealed class BossHuntEffect : SkillEffect
    {
        public BossHuntEffect(double bonus, double lastSeconds, double lastBonus)
        {
            Bonus = bonus;
            LastSeconds = lastSeconds;
            LastBonus = lastBonus;
            Modifiers = new[] { new Modifier(Sim.CombatStats.Damage, ModifierKind.More, 1) };
        }

        public IReadOnlyList<Modifier> Modifiers { get; }

        public double Bonus { get; }
        public double LastSeconds { get; }
        public double LastBonus { get; }
    }

    public sealed class ComposureEffect : SkillEffect
    {
        public ComposureEffect(double multiplier)
        {
            Multiplier = multiplier;
        }

        public double Multiplier { get; }
    }

    public sealed class PassiveEffect : SkillEffect
    {
        public PassiveEffect(IReadOnlyList<Modifier> modifiers, bool perPartyMember)
        {
            Modifiers = modifiers;
            PerPartyMember = perPartyMember;
        }

        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool PerPartyMember { get; }
    }

    public sealed class BreakBonusEffect : SkillEffect
    {
        public BreakBonusEffect(double breakSeconds, double seconds, IReadOnlyList<Modifier> modifiers)
        {
            BreakSeconds = breakSeconds;
            Seconds = seconds;
            Modifiers = modifiers;
        }

        public double BreakSeconds { get; }
        public double Seconds { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class ResourceGainEffect : SkillEffect
    {
        public ResourceGainEffect(double amount)
        {
            Amount = amount;
        }

        public double Amount { get; }
    }

    public sealed class EchoEffect : SkillEffect
    {
        public EchoEffect(double power)
        {
            Power = power;
        }

        public double Power { get; }
    }

    public sealed class NextKillRewardEffect : SkillEffect
    {
        public NextKillRewardEffect(double multiplier, double shardChance, double shards)
        {
            Multiplier = multiplier;
            ShardChance = shardChance;
            Shards = shards;
        }

        public double Multiplier { get; }
        public double ShardChance { get; }
        public double Shards { get; }
    }

    public sealed class EnemyDebuffEffect : SkillEffect
    {
        public EnemyDebuffEffect(double damageTaken, double bossTimerSlow = 0)
        {
            DamageTaken = damageTaken;
            BossTimerSlow = bossTimerSlow;
        }

        public double DamageTaken { get; }
        public double BossTimerSlow { get; }
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

    public sealed class CurseFlowEffect : SkillEffect
    {
        public CurseFlowEffect(double hitsPerSecond)
        {
            HitsPerSecond = hitsPerSecond;
        }

        public double HitsPerSecond { get; }
    }

    public enum MinionKind
    {
        Cannon,
        Skeleton,
        Beast
    }

    public sealed class SummonEffect : SkillEffect
    {
        public SummonEffect(MinionKind kind, double seconds, double hitsPerSecond, double multiplier)
        {
            Kind = kind;
            Seconds = seconds;
            HitsPerSecond = hitsPerSecond;
            Multiplier = multiplier;
        }

        public MinionKind Kind { get; }
        public double Seconds { get; }
        public double HitsPerSecond { get; }
        public double Multiplier { get; }
    }

    public enum DotKind
    {
        Poison,
        Burn
    }

    public sealed class AddStacksEffect : SkillEffect
    {
        public AddStacksEffect(DotKind kind, double stacks, double perSecond, double multiplier)
        {
            Kind = kind;
            Stacks = stacks;
            PerSecond = perSecond;
            Multiplier = multiplier;
        }

        public DotKind Kind { get; }
        public double Stacks { get; }
        public double PerSecond { get; }
        public double Multiplier { get; }
    }

    public sealed class DetonateEffect : SkillEffect
    {
        public DetonateEffect(double seconds)
        {
            Seconds = seconds;
        }

        public double Seconds { get; }
    }
}
