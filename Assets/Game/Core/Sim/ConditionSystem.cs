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
        public const int FateBoth = 3;
        public StageTable? Stages { get; set; }
        public ForgeRules? Forge { get; set; }
        public Party.SynergySystem? Synergy { get; set; }

        private bool FourRoles()
        {
            var roles = Synergy?.PartyRoles ?? 0;
            var count = 0;
            while (roles != 0)
            {
                count += roles & 1;
                roles >>= 1;
            }
            return count >= 4;
        }
        private int _ticksSinceKeys = int.MaxValue / 2;

        public void NoteInput(in EffectiveInput input)
        {
            NoteActivity(input);
            if (_state.AfterFastKillTicks > 0)
                _state.AfterFastKillTicks--;
        }

        public void Tick()
        {
            var conditions = Current();
            NoteShares(conditions & ~ModifierConditions.Bit(ModifierCondition.InRift));
            _state.ConditionShares[(int)ModifierCondition.Day] = _state.NightOffline ? 0 : 0.5;
            _state.ConditionShares[(int)ModifierCondition.Night] = _state.NightOffline ? 1 : 0.5;
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

        public ulong Current() => BossConditions() | ResourceConditions() | MomentConditions() | WorldConditions() | _guardian.Conditions();

        private void NoteActivity(in EffectiveInput input)
        {
            var activity = _state.InputActivity;
            var step = 1.0 / System.Math.Max(1, _combat.Conditions.LeastInputSeconds * GameLoop.TicksPerSecond);
            _ticksSinceKeys = input.Hits > input.ButtonPresses ? 0 : _ticksSinceKeys + 1;
            var keys = _ticksSinceKeys < GameLoop.TicksPerSecond;
            var mouse = input.MousePx > 0 || _state.MouseIdleTicks < GameLoop.TicksPerSecond;
            var clicks = input.ButtonPresses > 0 || _state.ClicklessTicks < GameLoop.TicksPerSecond;
            Blend(activity, 0, keys, step);
            Blend(activity, 1, mouse, step);
            Blend(activity, 2, clicks, step);
            Blend(activity, 3, !keys && !mouse && !clicks, step);
        }

        private static void Blend(double[] activity, int kind, bool on, double step) =>
            activity[kind] += ((on ? 1.0 : 0.0) - activity[kind]) * step;

        private ulong WorldConditions()
        {
            var conditions = 0UL;
            var rules = _combat.Conditions;
            if (_state.ServerHour >= 0)
                conditions |= ModifierConditions.Bit(rules.IsDay(_state.ServerHour) ? ModifierCondition.Day : ModifierCondition.Night);
            if (_state.Stage < _state.RecordStage * rules.BelowRecordShare)
                conditions |= ModifierConditions.Bit(ModifierCondition.BelowRecord);
            NoteTemper();
            if (_state.AllWornTempered)
                conditions |= ModifierConditions.Bit(ModifierCondition.AllTempered);
            if (_state.Fate == FateBoth)
                conditions |= ModifierConditions.Bit(ModifierCondition.FateDamage) | ModifierConditions.Bit(ModifierCondition.FateGold);
            else if (_state.Fate >= 0)
                conditions |= ModifierConditions.Bit((ModifierCondition)((int)ModifierCondition.FateDamage + _state.Fate));
            conditions |= LeastInput();
            if (Stages == null)
                return conditions;
            var biome = BiomeRotation.Index(Stages, _state, _state.Stage);
            if (biome >= 0 && biome < 5)
                conditions |= ModifierConditions.Bit((ModifierCondition)((int)ModifierCondition.InBiome1 + biome));
            if (biome >= 0 && _state.BiomeFresh)
                conditions |= ModifierConditions.Bit(ModifierCondition.FreshBiome);
            return conditions;
        }

        private void NoteTemper()
        {
            var worn = _state.Gear.Worn;
            var held = _state.Weapons.Held;
            var count = held != null ? 1 : 0;
            var sum = held != null ? held.Forge.Temper : 0;
            var max = Forge?.MaxTemper ?? 0;
            var all = max > 0 && (held == null || held.Forge.Temper >= max - 1e-9);
            for (var i = 0; i < worn.Length; i++)
            {
                var item = worn[i];
                if (item == null)
                    continue;
                count++;
                sum += item.Forge.Temper;
                all &= max > 0 && item.Forge.Temper >= max - 1e-9;
            }
            _state.WornTemperPercent = count == 0 ? 0 : sum / count * 100;
            _state.AllWornTempered = count > 0 && all;
        }

        private ulong LeastInput()
        {
            var activity = _state.InputActivity;
            var least = -1;
            var total = 0.0;
            for (var i = 0; i < activity.Length; i++)
            {
                total += activity[i];
                if (least < 0 || activity[i] < activity[least])
                    least = i;
            }
            return total > 0 ? ModifierConditions.Bit((ModifierCondition)((int)ModifierCondition.LeastKeys + least)) : 0;
        }

        private ulong MomentConditions()
        {
            var rules = _combat.Conditions;
            var conditions = 0UL;
            if (_state.Focus >= _focus.Max - 1e-9)
                conditions |= ModifierConditions.Bit(ModifierCondition.FocusFull);
            conditions |= ModifierConditions.Bit(_state.PartyAllies > 0 ? ModifierCondition.InParty : ModifierCondition.Solo);
            if (_state.EnemyAlive && _state.EnemyShieldFloor.Sign > 0 && _state.EnemyHp > _state.EnemyShieldFloor)
                conditions |= ModifierConditions.Bit(ModifierCondition.BossShielded);
            if (_state.Rift.Inside || _state.Dungeons.Inside)
                conditions |= ModifierConditions.Bit(ModifierCondition.InRift);
            if (_state.MascotCallTicks > 0)
                conditions |= ModifierConditions.Bit(ModifierCondition.MascotCall);
            if (FourRoles())
                conditions |= ModifierConditions.Bit(ModifierCondition.FourRoles);
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
