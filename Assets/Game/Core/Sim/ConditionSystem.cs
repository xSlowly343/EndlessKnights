using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ConditionSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly SkillSystem _skills;
        private readonly FocusSystem _focus;
        private readonly GuardianWatch _guardian;

        public ConditionSystem(GameState state, StatSheet hero, CombatSystem combat, SkillSystem skills, FocusSystem focus)
        {
            _focus = focus;
            _skills = skills;
            _state = state;
            _hero = hero;
            _combat = combat;
            _guardian = new GuardianWatch(state);
        }

        public GuardianWatch Guardian => _guardian;

        public void NoteInput(in EffectiveInput input)
        {
            if (_state.AfterFastKillTicks > 0)
                _state.AfterFastKillTicks--;
        }

        public void Tick()
        {
            var conditions = Current();
            NoteShares(conditions & ~ModifierConditions.Bit(ModifierCondition.InRift));
            var auraStrength = _hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            _hero.SetConditions(conditions);
            if (_hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura) != auraStrength)
                _skills.RefreshAuraStrength();
        }

        private void NoteShares(ulong conditions)
        {
            var shares = _state.ConditionShares;
            var step = 1.0 / System.Math.Max(1, _combat.Conditions.ShareWindowSeconds * GameLoop.TicksPerSecond);
            for (var i = 0; i < shares.Length; i++)
            {
                var on = (conditions >> i & 1UL) != 0 ? 1.0 : 0.0;
                shares[i] += (on - shares[i]) * step;
            }
        }

        public ulong Current() => BossConditions() | ResourceConditions() | MomentConditions() | _guardian.Conditions();

        private ulong MomentConditions()
        {
            var rules = _combat.Conditions;
            var conditions = 0UL;
            if (_state.Focus >= _focus.Max - 1e-9)
                conditions |= ModifierConditions.Bit(ModifierCondition.FocusFull);
            if (_state.PartyAllies > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.InParty);
            if (_state.Rift.Inside || _state.Dungeons.Inside)
                conditions |= ModifierConditions.Bit(ModifierCondition.InRift);
            if (_state.AfterFastKillTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AfterFastKill);
            if (_state.AfterCircleTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AfterCircle);
            if (_state.AfterFlickTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.AfterFlick);
            if (_state.MouseIdleTicks < Ticks(rules.MouseMovingSeconds))
                conditions |= ModifierConditions.Bit(ModifierCondition.MouseMoving);
            conditions |= ModifierConditions.Bit(_state.InputStreakTicks * GameLoop.TickSeconds >= rules.LongStreakSeconds
                ? ModifierCondition.LongStreak
                : ModifierCondition.ShortStreak);
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
    }
}
