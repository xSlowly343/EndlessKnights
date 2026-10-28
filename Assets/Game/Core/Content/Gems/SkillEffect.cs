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
        public double LowHpBelow { get; set; }
        public double LowHpMultiplier { get; set; }
        public double BossMultiplier { get; set; }
        public bool LastKeyHit { get; set; }
        public double PauseSeconds { get; set; }
        public double PauseMultiplier { get; set; }
        public double BossTimerStopSeconds { get; set; }
    }

    public sealed class PotionOption
    {
        public PotionOption(IReadOnlyList<Modifier> modifiers, bool bossTimerStop, double strike, double bossScore, double normalScore)
        {
            Modifiers = modifiers;
            BossTimerStop = bossTimerStop;
            Strike = strike;
            BossScore = bossScore;
            NormalScore = normalScore;
        }

        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool BossTimerStop { get; }
        public double Strike { get; }
        public double BossScore { get; }
        public double NormalScore { get; }

        public double Score(bool boss) => boss ? BossScore : NormalScore;
    }

    public sealed class PotionEffect : SkillEffect
    {
        public PotionEffect(IReadOnlyList<PotionOption> options)
        {
            Options = options;
        }

        public IReadOnlyList<PotionOption> Options { get; }
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
        public bool Queue { get; set; }
    }

    public sealed class InputKindsEffect : SkillEffect
    {
        public InputKindsEffect(double perKind)
        {
            PerKind = perKind;
        }

        public double PerKind { get; }
    }

    public sealed class FlurryEffect : SkillEffect
    {
        public FlurryEffect(int hits, double seconds, double multiplier, int queueTargets)
        {
            Hits = hits;
            Seconds = seconds;
            Multiplier = multiplier;
            QueueTargets = queueTargets;
            HitsPerTick = System.Math.Max(1, (int)System.Math.Ceiling(hits / (seconds * Sim.GameLoop.TicksPerSecond) - 1e-9));
        }

        public int Hits { get; }
        public double Seconds { get; }
        public double Multiplier { get; }
        public int QueueTargets { get; }
        public int HitsPerTick { get; }
    }

    public sealed class PauseStrikeEffect : SkillEffect
    {
        public PauseStrikeEffect(double pauseSeconds, double multiplier)
        {
            PauseSeconds = pauseSeconds;
            Multiplier = multiplier;
        }

        public double PauseSeconds { get; }
        public double Multiplier { get; }
    }

    public sealed class ComboStacksEffect : SkillEffect
    {
        public ComboStacksEffect(int hitsPerStack, Modifier perStack, double maxTotal)
        {
            HitsPerStack = hitsPerStack;
            PerStack = perStack;
            MaxTotal = maxTotal;
            Modifiers = new[] { new Modifier(perStack.Stat, perStack.Kind, 1, perStack.Tags) };
        }

        public int HitsPerStack { get; }
        public Modifier PerStack { get; }
        public double MaxTotal { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class QueueHitsEffect : SkillEffect
    {
        public QueueHitsEffect(double share)
        {
            Share = share;
        }

        public double Share { get; }
        public int Targets { get; set; }
        public HitInput Input { get; set; }
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
        public int Hits { get; set; }
        public int QueueTargets { get; set; }
        public HitInput Input { get; set; }
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
        public int QueueTargets { get; set; }
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
        public SkillCondition? When { get; set; }
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
        public double StrikeMultiplier { get; set; }
        public StatTags Tags { get; set; }
    }

    public sealed class HitStrikeEffect : SkillEffect
    {
        public HitStrikeEffect(HitInput input, int every, double multiplier, int queueTargets)
        {
            Input = input;
            Every = every;
            Multiplier = multiplier;
            QueueTargets = queueTargets;
        }

        public HitInput Input { get; }
        public int Every { get; }
        public double Multiplier { get; }
        public int QueueTargets { get; }
    }

    public sealed class ScaledEffect : SkillEffect
    {
        public ScaledEffect(ScaleSource source, double per, double from, int maxSteps, IReadOnlyList<Modifier> perStep, IReadOnlyList<Modifier> modifiers, string? family)
        {
            Source = source;
            Per = per;
            From = from;
            MaxSteps = maxSteps;
            PerStep = perStep;
            Modifiers = modifiers;
            Family = family;
        }

        public ScaleSource Source { get; }
        public double Per { get; }
        public double From { get; }
        public int MaxSteps { get; }
        public IReadOnlyList<Modifier> PerStep { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public string? Family { get; }
    }

    public sealed class PulseEffect : SkillEffect
    {
        public PulseEffect(double everySeconds, double multiplier, int queueTargets, double mouseEverySeconds = 0)
        {
            MouseEverySeconds = mouseEverySeconds;
            EverySeconds = everySeconds;
            Multiplier = multiplier;
            QueueTargets = queueTargets;
        }

        public double EverySeconds { get; }
        public double MouseEverySeconds { get; }
        public double Multiplier { get; }
        public int QueueTargets { get; }
    }

    public sealed class ExtendFamilyEffect : SkillEffect
    {
        public ExtendFamilyEffect(string family, double seconds)
        {
            Family = family;
            Seconds = seconds;
        }

        public string Family { get; }
        public double Seconds { get; }
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

    public sealed class WeakenNextEffect : SkillEffect
    {
        public WeakenNextEffect(int enemies, double hpShare)
        {
            Enemies = enemies;
            HpShare = hpShare;
        }

        public int Enemies { get; }
        public double HpShare { get; }
    }
}
