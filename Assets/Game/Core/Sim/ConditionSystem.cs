using Game.Core.Content;
using Game.Core.Content.Synergy;
using Game.Core.Party;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ConditionSystem
    {
        private const int MarkConditions = ModifierConditions.LastMark - ModifierConditions.FirstMark + 1;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly SynergySystem _synergy;
        private readonly int[] _markMasks = new int[MarkConditions];
        private SynergyRules? _markRules;

        public ConditionSystem(GameState state, StatSheet hero, CombatSystem combat, SynergySystem synergy)
        {
            _state = state;
            _hero = hero;
            _combat = combat;
            _synergy = synergy;
        }

        public void Tick() => _hero.SetConditions(Current());

        public ulong Current() => BossConditions() | ResourceConditions() | MarkConditionsOn(_synergy.OwnMarks | _synergy.AllyMarks);

        private ulong BossConditions()
        {
            if (!_state.EnemyAlive || _state.EnemyType != EnemyType.Boss)
                return 0;
            var conditions = ModifierConditions.Bit(ModifierCondition.Boss);
            if (_state.EnemyLifeSeconds < _combat.Conditions.BossOpeningSeconds)
                conditions |= ModifierConditions.Bit(ModifierCondition.BossOpening);
            return conditions;
        }

        private ulong ResourceConditions()
        {
            var fill = _state.ResourceFill;
            var conditions = 0UL;
            if (fill > 0.3)
                conditions |= ModifierConditions.Bit(ModifierCondition.ResourceAbove30);
            if (fill > 0.5)
                conditions |= ModifierConditions.Bit(ModifierCondition.ResourceAbove50);
            if (fill >= 0.8)
                conditions |= ModifierConditions.Bit(ModifierCondition.ResourceAbove80);
            if (fill >= 1)
                conditions |= ModifierConditions.Bit(ModifierCondition.ResourceFull);
            return conditions;
        }

        private ulong MarkConditionsOn(int marks)
        {
            if (marks == 0)
                return 0;
            MapMarks();
            var conditions = 0UL;
            for (var i = 0; i < MarkConditions; i++)
            {
                if ((marks & _markMasks[i]) != 0)
                    conditions |= ModifierConditions.Bit(ModifierConditions.Mark(i));
            }
            return conditions;
        }

        private void MapMarks()
        {
            var rules = _synergy.Rules;
            if (ReferenceEquals(rules, _markRules))
                return;
            _markRules = rules;
            for (var i = 0; i < MarkConditions; i++)
                _markMasks[i] = rules.MarkMask(ModifierConditions.MarkName(ModifierConditions.Mark(i)));
        }
    }
}
