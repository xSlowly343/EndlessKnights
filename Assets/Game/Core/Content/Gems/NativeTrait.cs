using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class NativeTrait
    {
        public static readonly NativeTrait None = new NativeTrait();

        public IReadOnlyList<Modifier> Modifiers { get; set; } = System.Array.Empty<Modifier>();
        public double HoldDecaySeconds { get; set; }
        public double SpendAllPerPoint { get; set; }
        public double SpendAllMax { get; set; }
        public double ResourcePerKeyHit { get; set; }
        public double WoundedSpawnResource { get; set; }
        public double HoldStacksAboveResource { get; set; }
        public double FullResourceDurationMultiplier { get; set; }
        public string? ExtraTargetWhileFamily { get; set; }
        public string? RefreshFamily { get; set; }
        public double FreeAboveCombo { get; set; }
        public bool CarryOn { get; set; }
        public bool HoldCombo { get; set; }
        public double FullResourceBonus { get; set; }
        public double RepeatAboveCombo { get; set; }
        public double BossReleaseShare { get; set; }
        public double ResourceOnCast { get; set; }
        public double ResourceOnKill { get; set; }
        public double ResourceOnHit { get; set; }
        public double ResourceOnCrit { get; set; }
        public double ResourcePerPoisonTick { get; set; }
        public double MouseEverySeconds { get; set; }
        public double ResourceOnEnd { get; set; }
        public double WeakenedGold { get; set; }
        public double StackSeconds { get; set; }
        public bool CarryOver { get; set; }
        public double WindowSeconds { get; set; }
        public double StackGrowth { get; set; }
        public double ResourceRelief { get; set; }
        public double PerMinion { get; set; }
        public int Barrels { get; set; }
        public bool RespawnMinion { get; set; }
        public int RepeatEvery { get; set; }
        public double WhenResource { get; set; }
        public double ResourcePerSavedSecond { get; set; }
        public bool RestoresFocus { get; set; }
        public double BossTimerSeconds { get; set; }
        public double LastsAboveResource { get; set; }
        public double WeakenShare { get; set; }
        public bool PartyStacks { get; set; }
        public double ExtraSecondsPerPoint { get; set; }
        public bool BestOfTwo { get; set; }
        public int QueueTargets { get; set; }
        public double ResetWithinSeconds { get; set; }
        public HitInput? RepeatInput { get; set; }
        public double ExtraHitPerMousePx { get; set; }
        public int MaxHits { get; set; }
        public double ReleaseBonusPx { get; set; }
        public double ReleaseBonus { get; set; }
        public int ComboOnCast { get; set; }
        public int ComboPerHit { get; set; }
        public int ComboPerKeyHit { get; set; }
        public double DurationMultiplier { get; set; }
        public SkillCondition? When { get; set; }
        public IReadOnlyList<Modifier> WhenModifiers { get; set; } = System.Array.Empty<Modifier>();
        public double WhenMultiplier { get; set; }
        public double WhenExecuteBelow { get; set; }
        public bool WhenFree { get; set; }
        public double WhenDurationMultiplier { get; set; }
        public int WhenExtraTargets { get; set; }

        public bool SpendsAll => SpendAllPerPoint > 0 || ExtraSecondsPerPoint > 0;
        public bool HoldsStacks => HoldStacksAboveResource > 0;
    }
}
