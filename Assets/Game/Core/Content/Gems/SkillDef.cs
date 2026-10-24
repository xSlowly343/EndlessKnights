using System.Collections.Generic;

namespace Game.Core.Content.Gems
{
    public sealed class SkillDef
    {
        public SkillDef(double resourceCost, double cooldownSeconds, double durationSeconds, SkillEffect effect, IReadOnlyList<string>? marks = null, string? weapon = null, NativeTrait? native = null, string? family = null)
        {
            Family = family;
            Native = native ?? NativeTrait.None;
            Weapon = weapon;
            Marks = marks ?? System.Array.Empty<string>();
            ResourceCost = resourceCost;
            CooldownSeconds = cooldownSeconds;
            DurationSeconds = durationSeconds;
            Effect = effect;
        }

        public double ResourceCost { get; }
        public double CooldownSeconds { get; }
        public double DurationSeconds { get; }
        public SkillEffect Effect { get; }
        public IReadOnlyList<string> Marks { get; }
        public string? Weapon { get; }
        public NativeTrait Native { get; }
        public bool IsPassive => Effect is IdleStrikeEffect || Reserves || (Effect is StacksEffect stacks && stacks.Passive);
        public bool Reserves => Effect is AuraEffect aura && aura.Reserves;

        public string? Family { get; }
    }
}
