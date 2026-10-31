using Game.Core.Content.Gems;

namespace Game.Core.Sim
{
    public sealed class TriggerSystem
    {
        private readonly GameState _state;
        private readonly SkillSystem _skills;
        private readonly ResourceSystem _resource;
        private long _seenKills;
        private long _seenBossKills;
        private long _seenBossStarts;
        private int _newKills;
        private bool _bossKilled;
        private bool _bossStarted;
        private double _allyHeavySeconds;

        public TriggerSystem(GameState state, SkillSystem skills, ResourceSystem resource)
        {
            _state = state;
            _skills = skills;
            _resource = resource;
            _seenKills = state.Kills;
            _seenBossKills = state.BossKills;
            _seenBossStarts = state.BossStarts;
        }

        public void Tick(in EffectiveInput input)
        {
            TakeEvents();
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                var trigger = slot.Trigger?.Trigger;
                if (trigger == null)
                    continue;
                if (Fires(slot, index, trigger, input) && _skills.TryCast(index, triggered: true) == CastResult.Cast)
                    slot.TriggerProgress = 0;
            }
        }

        private void TakeEvents()
        {
            _newKills = (int)System.Math.Min(int.MaxValue, System.Math.Max(0, _state.Kills - _seenKills));
            _bossKilled = _state.BossKills > _seenBossKills;
            _bossStarted = _state.BossStarts > _seenBossStarts;
            _seenKills = _state.Kills;
            _seenBossKills = _state.BossKills;
            _seenBossStarts = _state.BossStarts;
            _allyHeavySeconds = _state.AllyHeavyCastSeconds;
            _state.AllyHeavyCastSeconds = 0;
        }

        private bool Fires(SkillSlot slot, int index, TriggerDef trigger, in EffectiveInput input)
        {
            switch (trigger.Condition)
            {
                case TriggerCondition.MouseDistance:
                    return Accumulate(slot, input.MousePx, trigger.Value);
                case TriggerCondition.Circle:
                    return Accumulate(slot, input.CircleTurns, trigger.Value);
                case TriggerCondition.EveryKills:
                    return Accumulate(slot, _newKills, trigger.Value);
                case TriggerCondition.BossTimerLeft:
                    return _state.FightingBoss && _state.EnemyAlive && _state.BossSecondsLeft < trigger.Value;
                case TriggerCondition.Idle:
                    return _state.IdleTicks * GameLoop.TickSeconds >= trigger.Value;
                case TriggerCondition.BossKilled:
                    return _bossKilled;
                case TriggerCondition.BossSpawned:
                    return _bossStarted;
                case TriggerCondition.ResourceFull:
                    return _resource.IsFull;
                case TriggerCondition.ResourceBelow:
                    return _state.Resource < trigger.Value;
                case TriggerCondition.WithLeft:
                    return CastWith(index, SkillLoadout.IndexOf(_state.Skills, SkillSlotRole.Left));
                case TriggerCondition.WithRight:
                    return CastWith(index, SkillLoadout.IndexOf(_state.Skills, SkillSlotRole.Right));
                case TriggerCondition.Flick:
                    return input.Flicks > 0;
                case TriggerCondition.AllyHeavyCast:
                    return _allyHeavySeconds >= trigger.Value;
                case TriggerCondition.Every:
                    return (_state.Tick - slot.QuietSince) * GameLoop.TickSeconds >= trigger.Value;
                default:
                    return false;
            }
        }

        private static bool Accumulate(SkillSlot slot, double amount, double threshold)
        {
            slot.TriggerProgress = System.Math.Min(threshold, slot.TriggerProgress + amount);
            return slot.TriggerProgress >= threshold;
        }

        private bool CastWith(int index, int buttonSlot) =>
            index != buttonSlot && buttonSlot >= 0 && buttonSlot < _state.Skills.Count && _state.Skills[buttonSlot].CastOn(_state.Tick);
    }
}
