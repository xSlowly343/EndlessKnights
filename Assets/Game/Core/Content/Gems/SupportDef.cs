namespace Game.Core.Content.Gems
{
    public enum SupportKind
    {
        Rebound,
        Chain,
        Executioner,
        Apprentice,
        BossHunter,
        DeathMark
    }

    public sealed class SupportDef
    {
        private SupportDef(SupportKind kind)
        {
            Kind = kind;
        }

        public SupportKind Kind { get; }
        public double CooldownMultiplier { get; private set; } = 1;
        public double KillCooldownSeconds { get; private set; }
        public double QueueShare { get; private set; }
        public double LowHpBelow { get; private set; }
        public double LowHpMultiplier { get; private set; } = 1;
        public double Power { get; private set; } = 1;
        public double ExperienceMultiplier { get; private set; } = 1;
        public double BossMultiplier { get; private set; } = 1;
        public double NormalMultiplier { get; private set; } = 1;
        public double KillResource { get; private set; }
        public double MarkedMultiplier { get; private set; } = 1;

        public static SupportDef? Read(ContentNode effect)
        {
            switch (effect.Field("type").AsString())
            {
                case "rebound":
                    return new SupportDef(SupportKind.Rebound)
                    {
                        CooldownMultiplier = effect.Field("cooldownMultiplier").AsNumber(0.1, 1),
                        KillCooldownSeconds = effect.Field("killCooldownSeconds").AsNumber(0, 600)
                    };
                case "chain":
                    return new SupportDef(SupportKind.Chain) { QueueShare = effect.Field("share").AsNumber(0, 1) };
                case "executioner":
                    return new SupportDef(SupportKind.Executioner)
                    {
                        KillCooldownSeconds = effect.Field("killCooldownSeconds").AsNumber(0, 600),
                        LowHpBelow = effect.Field("below").AsNumber(0, 1),
                        LowHpMultiplier = effect.Field("multiplier").AsNumber(1, 1000)
                    };
                case "apprentice":
                    return new SupportDef(SupportKind.Apprentice)
                    {
                        Power = effect.Field("power").AsNumber(0, 10),
                        ExperienceMultiplier = effect.Field("experienceMultiplier").AsNumber(0, 100)
                    };
                case "bossHunter":
                    return new SupportDef(SupportKind.BossHunter)
                    {
                        BossMultiplier = effect.Field("boss").AsNumber(0, 100),
                        NormalMultiplier = effect.Field("normal").AsNumber(0, 100)
                    };
                case "deathMark":
                    return new SupportDef(SupportKind.DeathMark)
                    {
                        KillResource = effect.Field("killResource").AsNumber(0, 1000),
                        MarkedMultiplier = effect.Field("markedMultiplier").AsNumber(0, 100)
                    };
                default:
                    return null;
            }
        }
    }
}
