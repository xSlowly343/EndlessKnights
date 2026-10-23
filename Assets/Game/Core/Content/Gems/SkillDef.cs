namespace Game.Core.Content.Gems
{
    public sealed class SkillDef
    {
        public SkillDef(double resourceCost, double cooldownSeconds, double durationSeconds, SkillEffect effect)
        {
            ResourceCost = resourceCost;
            CooldownSeconds = cooldownSeconds;
            DurationSeconds = durationSeconds;
            Effect = effect;
        }

        public double ResourceCost { get; }
        public double CooldownSeconds { get; }
        public double DurationSeconds { get; }
        public SkillEffect Effect { get; }
        public bool IsPassive => Effect is IdleStrikeEffect;
    }
}
