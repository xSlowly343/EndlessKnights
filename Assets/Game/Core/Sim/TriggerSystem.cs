using Game.Core.Content.Gems;

namespace Game.Core.Sim
{
    public sealed class TriggerSystem
    {
        private readonly GameState _state;
        private readonly SkillSystem _skills;

        public TriggerSystem(GameState state, SkillSystem skills)
        {
            _state = state;
            _skills = skills;
        }

        public void Tick(in EffectiveInput input)
        {
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var trigger = _state.Skills[index].Trigger?.Trigger;
                if (trigger != null && Fires(trigger.Condition, input))
                    _skills.TryCast(index, triggered: true);
            }
        }

        private static bool Fires(TriggerCondition condition, in EffectiveInput input)
        {
            switch (condition)
            {
                case TriggerCondition.Enter:
                    return input.EnterDowns > 0;
                default:
                    return false;
            }
        }
    }
}
