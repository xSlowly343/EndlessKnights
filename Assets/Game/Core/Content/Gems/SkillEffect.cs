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
        public StacksEffect? Stacks { get; set; }
        public bool ConsumeMinion { get; set; }
        public double BurnStacks { get; set; }
        public int Runes { get; set; }
        public bool Word { get; set; }
        public int Blades { get; set; }
        public bool BladeRain { get; set; }
        public double WhirlSeconds { get; set; }
        public bool Breach { get; set; }
        public int Touches { get; set; }
        public double PhantomSeconds { get; set; }
        public double Tension { get; set; }
        public int Gusts { get; set; }
        public double CalmSeconds { get; set; }
        public bool Shot { get; set; }
        public int Crystals { get; set; }
        public double Shatter { get; set; }
        public int Grow { get; set; }
        public bool Crack { get; set; }
        public double PrisonSeconds { get; set; }
        public double HeartSeconds { get; set; }
        public double Resonance { get; set; }
        public double PerCrystal { get; set; }
        public double Sand { get; set; }
        public int Pour { get; set; }
        public bool Camp { get; set; }
        public int Mirages { get; set; }
        public double TopHit { get; set; }
        public double AfterStep { get; set; }
        public double StepHit { get; set; }
        public double BlindSeconds { get; set; }
        public double TimerSeconds { get; set; }
        public int Lines { get; set; }
        public int Notes { get; set; }
        public bool Tune { get; set; }
        public double ClimaxHit { get; set; }
        public double Finale { get; set; }
        public double HymnSeconds { get; set; }
        public double HymnPower { get; set; }
        public double SoloSeconds { get; set; }
        public double Dream { get; set; }
        public double CaughtHit { get; set; }
        public int Nets { get; set; }
        public int AlbumAdd { get; set; }
        public int Bait { get; set; }
        public double DozeSeconds { get; set; }
        public double LucidSeconds { get; set; }
        public double LitHit { get; set; }
        public double Oil { get; set; }
        public double Light { get; set; }
        public int Fireflies { get; set; }
        public bool Flash { get; set; }
        public double GlowSeconds { get; set; }
        public double GlowPerOil { get; set; }
        public double Curse { get; set; }
        public double CurseSeconds { get; set; }
        public double CurseHit { get; set; }
        public double ExtendCurse { get; set; }
        public double Whisper { get; set; }
        public int Knots { get; set; }
        public bool Seventh { get; set; }
        public int NameHits { get; set; }
        public int Hush { get; set; }
        public double Wind { get; set; }
        public double WindHit { get; set; }
        public double HoldSeconds { get; set; }
        public double CutCooldowns { get; set; }
        public double WindBurst { get; set; }
        public bool Rewind { get; set; }
        public bool GearTick { get; set; }
        public int Reagents { get; set; }
        public bool Flask { get; set; }
        public int Layers { get; set; }
        public bool Elixir { get; set; }
        public double Corrode { get; set; }
        public double CorrodeSeconds { get; set; }
        public double VeinHits { get; set; }
        public double Luck { get; set; }
        public int Nuggets { get; set; }
        public double GoldHit { get; set; }
        public double VeinGold { get; set; }
        public int Souls { get; set; }
        public double SpiritsHit { get; set; }
        public bool ReleaseAll { get; set; }
        public bool Lend { get; set; }
        public bool Breathe { get; set; }
        public int Cannons { get; set; }
        public bool Volley { get; set; }
        public int Sailors { get; set; }
        public double CrewHit { get; set; }
        public bool ParrotRepeat { get; set; }
        public int Moths { get; set; }
        public double SwarmHit { get; set; }
        public bool Wake { get; set; }
        public int Cards { get; set; }
        public bool Arcana { get; set; }
        public bool Reveal { get; set; }
        public double HandHit { get; set; }
        public bool Forecast { get; set; }
        public int Seeds { get; set; }
        public bool Bloom { get; set; }
        public double Venom { get; set; }
        public double VenomBurst { get; set; }
        public bool Drink { get; set; }
        public int SealHits { get; set; }
        public double GolemHit { get; set; }
        public double GolemSeconds { get; set; }
        public bool CompleteSeal { get; set; }
        public int GolemRunes { get; set; }
        public int Golemlings { get; set; }
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
        public double IdleOffSeconds { get; set; }
        public int SummonEveryKeys { get; set; }
        public double AllyResource { get; set; }
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
        public int Touches { get; set; }
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
        public double BurnStacks { get; set; }
    }

    public enum SpecialKey
    {
        Space,
        Backspace,
        Combo
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

    public enum MinionKind
    {
        Skeleton,
        Beast
    }

    public enum DotKind
    {
        Poison,
        Burn
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
        public bool Party { get; set; }
        public bool Passive => Trigger == StackTrigger.Kill;

        public Modifier For(int stacks) =>
            new Modifier(PerStack.Stat, PerStack.Kind, System.Math.Min(MaxTotal, PerStack.Value * stacks), PerStack.Tags);
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
}
