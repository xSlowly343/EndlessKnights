using System.Collections.Generic;
using Game.Core.Content.Gems;
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
        public int ActiveTicks { get; set; }
        public int HitsLeft { get; set; }
        public bool AuraApplied { get; set; }
        public List<Modifier> AuraModifiers { get; } = new List<Modifier>();

        public SkillDef Def => Skill.Skill!;
        public bool Ready => CooldownTicks <= 0;
        public bool Active => ActiveTicks > 0;
    }
}
