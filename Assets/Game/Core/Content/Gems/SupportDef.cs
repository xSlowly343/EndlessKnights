namespace Game.Core.Content.Gems
{
    public enum SupportKind
    {
        Rebound,
        Chain,
        Executioner
    }

    public sealed class SupportDef
    {
        private SupportDef(SupportKind kind, double cooldownMultiplier, double killCooldownSeconds, double queueShare, double lowHpBelow, double lowHpMultiplier)
        {
            Kind = kind;
            CooldownMultiplier = cooldownMultiplier;
            KillCooldownSeconds = killCooldownSeconds;
            QueueShare = queueShare;
            LowHpBelow = lowHpBelow;
            LowHpMultiplier = lowHpMultiplier;
        }

        public SupportKind Kind { get; }
        public double CooldownMultiplier { get; }
        public double KillCooldownSeconds { get; }
        public double QueueShare { get; }
        public double LowHpBelow { get; }
        public double LowHpMultiplier { get; }

        public static SupportDef? Read(ContentNode effect)
        {
            switch (effect.Field("type").AsString())
            {
                case "rebound":
                    return new SupportDef(SupportKind.Rebound,
                        effect.Field("cooldownMultiplier").AsNumber(0.1, 1),
                        effect.Field("killCooldownSeconds").AsNumber(0, 600), 0, 0, 1);
                case "chain":
                    return new SupportDef(SupportKind.Chain, 1, 0, effect.Field("share").AsNumber(0, 1), 0, 1);
                case "executioner":
                    return new SupportDef(SupportKind.Executioner, 1,
                        effect.Field("killCooldownSeconds").AsNumber(0, 600), 0,
                        effect.Field("below").AsNumber(0, 1),
                        effect.Field("multiplier").AsNumber(1, 1000));
                default:
                    return null;
            }
        }
    }
}
