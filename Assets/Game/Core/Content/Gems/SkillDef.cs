namespace Game.Core.Content.Gems
{
    public sealed class SkillDef
    {
        public SkillDef(double energyCost, int noteCost, double cooldownSeconds, double durationSeconds, SkillEffect effect)
        {
            EnergyCost = energyCost;
            NoteCost = noteCost;
            CooldownSeconds = cooldownSeconds;
            DurationSeconds = durationSeconds;
            Effect = effect;
        }

        public double EnergyCost { get; }
        public int NoteCost { get; }
        public double CooldownSeconds { get; }
        public double DurationSeconds { get; }
        public SkillEffect Effect { get; }
        public bool IsPassive => Effect is IdleStrikeEffect;
    }
}
