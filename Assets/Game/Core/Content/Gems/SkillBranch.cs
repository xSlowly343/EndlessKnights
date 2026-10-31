namespace Game.Core.Content.Gems
{
    public sealed class SkillBranch
    {
        public SkillBranch(string id, string nameRu, string nameEn, double damage, double cooldown, double cost, double duration, SkillEffect? effect)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            DamageMultiplier = damage;
            CooldownMultiplier = cooldown;
            CostMultiplier = cost;
            DurationMultiplier = duration;
            Effect = effect;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double DamageMultiplier { get; }
        public double CooldownMultiplier { get; }
        public double CostMultiplier { get; }
        public double DurationMultiplier { get; }
        public SkillEffect? Effect { get; }
    }
}
