namespace Game.Core.Content.Gems
{
    public sealed class TriggerDef
    {
        public TriggerDef(TriggerCondition condition, double value, double cooldownMultiplier)
        {
            Condition = condition;
            Value = value;
            CooldownMultiplier = cooldownMultiplier;
        }

        public TriggerCondition Condition { get; }
        public double Value { get; }
        public double CooldownMultiplier { get; }
    }
}
