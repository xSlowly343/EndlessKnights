using Game.Core.Content;
using Game.Core.Content.Synergy;
using Game.Core.Party;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ConditionSystem
    {
        private static readonly ulong AllyMask = AllyMarkConditions();

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly SynergySystem _synergy;
        private readonly SkillSystem _skills;
        private readonly int[] _markMasks = new int[ModifierConditions.Marks];
        private SynergyRules? _markRules;
        private ulong _allyConditions;

        public ConditionSystem(GameState state, StatSheet hero, CombatSystem combat, SynergySystem synergy, SkillSystem skills)
        {
            _skills = skills;
            _state = state;
            _hero = hero;
            _combat = combat;
            _synergy = synergy;
        }

        public void Tick()
        {
            var conditions = Current();
            var auraStrength = _hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            var curseStrength = _hero.Value(CombatStats.CurseEffect, 1);
            _hero.SetConditions(conditions);
            if (_hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura) != auraStrength)
                _skills.RefreshAuraStrength();
            if (_hero.Value(CombatStats.CurseEffect, 1) != curseStrength)
                _skills.RefreshCurseStrength();
            GrantOnRisingAllyMarks(conditions);
        }

        private void GrantOnRisingAllyMarks(ulong conditions)
        {
            var allies = conditions & AllyMask;
            var rising = allies & ~_allyConditions;
            _allyConditions = allies;
            if (rising != 0)
                _state.PendingResource += System.Math.Max(0, _hero.FlatWhen(CombatStats.ResourceOnAllyMark, rising));
        }

        private static ulong AllyMarkConditions()
        {
            var mask = 0UL;
            for (var i = 0; i < ModifierConditions.Marks; i++)
                mask |= ModifierConditions.Bit(ModifierConditions.AllyMark(i));
            return mask;
        }

        public ulong Current() => BossConditions() | ResourceConditions() | MarkConditionsOn(_synergy.OwnMarks, _synergy.AllyMarks);

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
            if (_state.Resource > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.HasResource);
            if (_state.Reading)
                conditions |= ModifierConditions.Bit(ModifierCondition.Reading);
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

        private ulong MarkConditionsOn(int own, int allies)
        {
            if ((own | allies) == 0)
                return 0;
            MapMarks();
            var conditions = 0UL;
            for (var i = 0; i < ModifierConditions.Marks; i++)
            {
                if (((own | allies) & _markMasks[i]) != 0)
                    conditions |= ModifierConditions.Bit(ModifierConditions.Mark(i));
                if ((allies & _markMasks[i]) != 0)
                    conditions |= ModifierConditions.Bit(ModifierConditions.AllyMark(i));
            }
            return conditions;
        }

        private void MapMarks()
        {
            var rules = _synergy.Rules;
            if (ReferenceEquals(rules, _markRules))
                return;
            _markRules = rules;
            for (var i = 0; i < ModifierConditions.Marks; i++)
                _markMasks[i] = rules.MarkMask(ModifierConditions.MarkName(ModifierConditions.Mark(i)));
        }
    }
}
