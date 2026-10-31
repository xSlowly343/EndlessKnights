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
        private readonly FocusSystem _focus;
        private readonly int[] _markMasks = new int[ModifierConditions.Marks];
        private SynergyRules? _markRules;
        private ulong _allyConditions;

        public ConditionSystem(GameState state, StatSheet hero, CombatSystem combat, SynergySystem synergy, SkillSystem skills, FocusSystem focus)
        {
            _focus = focus;
            _skills = skills;
            _state = state;
            _hero = hero;
            _combat = combat;
            _synergy = synergy;
        }

        public void NoteInput(in EffectiveInput input)
        {
            var rules = _combat.Conditions;
            _state.AfterCircleTicks = input.CircleTurns >= 1 ? Ticks(rules.AfterGestureSeconds) : System.Math.Max(0, _state.AfterCircleTicks - 1);
            _state.AfterFlickTicks = input.Flicks > 0 ? Ticks(rules.AfterGestureSeconds) : System.Math.Max(0, _state.AfterFlickTicks - 1);
            if (_state.AfterFastKillTicks > 0)
                _state.AfterFastKillTicks--;
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

        public ulong Current() => BossConditions() | ResourceConditions() | MarkConditionsOn(_synergy.OwnMarks, _synergy.AllyMarks) | MomentConditions();

        private ulong MomentConditions()
        {
            var rules = _combat.Conditions;
            var conditions = 0UL;
            if (_state.Focus >= _focus.Max - 1e-9)
                conditions |= ModifierConditions.Bit(ModifierCondition.FocusFull);
            if (_state.PartyAllies > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.InParty);
            if (_state.AfterFastKillTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AfterFastKill);
            if (_state.AfterCircleTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AfterCircle);
            if (_state.AfterFlickTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AfterFlick);
            if (_state.MouseIdleTicks < Ticks(rules.MouseMovingSeconds))
                conditions |= ModifierConditions.Bit(ModifierCondition.MouseMoving);
            if (_state.InputStreakTicks * GameLoop.TickSeconds >= rules.LongStreakSeconds)
                conditions |= ModifierConditions.Bit(ModifierCondition.LongStreak);
            return conditions;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);

        private ulong BossConditions()
        {
            if (!_state.EnemyAlive || _state.EnemyType != EnemyType.Boss)
                return 0;
            var conditions = ModifierConditions.Bit(ModifierCondition.Boss);
            var rules = _combat.Conditions;
            if (_state.EnemyLifeSeconds < rules.BossOpeningSeconds)
                conditions |= ModifierConditions.Bit(ModifierCondition.BossOpening);
            if (_state.EnemyMaxHp.Sign > 0 && (_state.EnemyHp / _state.EnemyMaxHp).ToDouble() < rules.BossLowHpShare)
                conditions |= ModifierConditions.Bit(ModifierCondition.BossLowHp);
            if (_state.BossSecondsLeft <= rules.BossTimerEndSeconds)
                conditions |= ModifierConditions.Bit(ModifierCondition.BossTimerEnd);
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
            if (allies != 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AnyAllyMark);
            var marked = 0;
            for (var i = 0; i < ModifierConditions.Marks; i++)
            {
                if (((own | allies) & _markMasks[i]) != 0)
                {
                    conditions |= ModifierConditions.Bit(ModifierConditions.Mark(i));
                    marked++;
                }
                if ((allies & _markMasks[i]) != 0)
                    conditions |= ModifierConditions.Bit(ModifierConditions.AllyMark(i));
            }
            if (marked >= 2)
                conditions |= ModifierConditions.Bit(ModifierCondition.TwoMarks);
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
