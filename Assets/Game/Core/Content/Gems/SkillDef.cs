using System.Collections.Generic;

namespace Game.Core.Content.Gems
{
    public sealed class SkillDef
    {
        public SkillDef(double resourceCost, double cooldownSeconds, double durationSeconds, SkillEffect effect, IReadOnlyList<string>? marks = null, string? weapon = null)
        {
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
        public bool IsPassive => Effect is IdleStrikeEffect;
    }
}
