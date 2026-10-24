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
        }

        public GemDef Skill { get; set; }
        public GemDef? Trigger { get; set; }
        public int CooldownTicks { get; set; }
        public int CooldownTotalTicks { get; set; }
        public int ActiveTicks { get; set; }
        public int ActiveTotalTicks { get; set; }
        public int HitsLeft { get; set; }
        public BigNumber Stored { get; set; }
        public int Stacks { get; set; }
        public int StackTicks { get; set; }
        public Modifier StackModifier { get; set; }
        public double ReservedApplied { get; set; }
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
    }
}
