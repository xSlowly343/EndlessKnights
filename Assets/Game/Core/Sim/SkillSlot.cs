using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SkillSlot
    {
        public SkillSlot(GemDef skill, GemDef? trigger = null)
        {
            Skill = skill;
            Trigger = trigger;
            if (skill.Skill?.Effect is DistanceStacksEffect distance)
                Buckets = new double[distance.WindowSeconds];
        }

        public GemDef Skill { get; set; }
        public GemDef? Trigger { get; set; }
        public int CooldownTicks { get; set; }
        public int CooldownTotalTicks { get; set; }
        public int ActiveTicks { get; set; }
        public int ActiveTotalTicks { get; set; }
        public long EquipTick { get; set; }
        public long LastCastTick { get; set; } = -1;
        public double TriggerProgress { get; set; }
        public int HitCount { get; set; }
        public long LastKeyTick { get; set; } = -1;
        public long FlowStartTick { get; set; } = -1;
        public int HitsLeft { get; set; }
        public BigNumber Stored { get; set; }
        public int Stacks { get; set; }
        public int StackTicks { get; set; }
        public Modifier StackModifier { get; set; }
        public double ReservedApplied { get; set; }
        public bool Primed { get; set; }
        public double PrimedSpent { get; set; }
        public bool PrimedWasFull { get; set; }
        public double Progress { get; set; }
        public double CircleProgress { get; set; }
        public double[]? Buckets { get; set; }
        public long BucketSecond { get; set; } = -1;
        public long ReadyTick { get; set; }
        public double CastPower { get; set; } = 1;
        public double PassiveScale { get; set; }
        public double AuraPower { get; set; } = 1;
        public void StartCooldown(int ticks)
        {
            CooldownTicks = ticks;
            CooldownTotalTicks = ticks;
        }

        public bool AuraApplied { get; set; }
        public double AuraStrength { get; set; }
        public List<Modifier> AuraModifiers { get; } = new List<Modifier>();

        public SkillDef Def => Skill.Skill!;
        public bool Ready => CooldownTicks <= 0;
        public double CooldownFraction => Ready ? 0 : CooldownTicks / (double)System.Math.Max(CooldownTicks, CooldownTotalTicks);
        public bool Active => ActiveTicks > 0;
        public bool CastOn(long tick) => LastCastTick == tick;
        public long QuietSince => System.Math.Max(EquipTick, LastCastTick);
    }
}
