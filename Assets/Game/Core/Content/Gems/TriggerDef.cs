namespace Game.Core.Content.Gems
{
    public sealed class TriggerDef
    {
        public TriggerDef(TriggerCondition condition, double cooldownMultiplier)
        {
            Condition = condition;
            CooldownMultiplier = cooldownMultiplier;
        }

        public TriggerCondition Condition { get; }
        public double CooldownMultiplier { get; }
    }
}
