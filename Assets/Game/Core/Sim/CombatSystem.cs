using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class CombatSystem
    {
        private readonly StatSheet _hero;
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly ClassMechanics _class;
        private CombatRules _rules;
        private IKillHandler? _kills;
        private IKeyHitListener? _keyHits;
        private long _hitsTowardCrit;
        private double _extraCritChance;

        public SkillSlot? Supporting { get; set; }

        public CombatSystem(CombatRules rules, StatSheet hero, GameState state, EventQueue events)
        {
            _rules = rules;
            _hero = hero;
            _state = state;
            _events = events;
            _class = new ClassMechanics(rules.Classes, state, hero);
        }

        public ClassMechanics Class => _class;
        public BigNumber LastTickDamage { get; private set; }

        public void UseRules(CombatRules rules)
        {
            _rules = rules;
            _class.UseRules(rules.Classes);
        }

        public void OnKill(IKillHandler kills) => _kills = kills;

        public void OnKeyHit(IKeyHitListener keyHits) => _keyHits = keyHits;

        public double BaseKeyDamage => _rules.KeyDamage;
        public CurseRules Curses => _rules.Curses;
        public ConditionRules Conditions => _rules.Conditions;
        public CannonRules Cannons => _rules.Cannons;
        public DotRules Dots => _rules.Dots;
        public MinionRules Skeletons => _rules.Skeletons;
        public MinionRules Beasts => _rules.Beasts;
        public BannerRules Banner => _rules.Banner;
        public ClassRules Classes => _rules.Classes;
        public PathRules Paths => _rules.Paths;
        public bool LastHitCrit { get; private set; }

        public void SkillHit(double baseDamage, double multiplier, StatTags tags) =>
            Hit(DamageSource.Skill, baseDamage, tags, HitFlags.None, multiplier);

        public void SkillHit(double baseDamage, double multiplier, StatTags tags, double extraCritChance)
        {
            _extraCritChance = extraCritChance;
            Hit(DamageSource.Skill, baseDamage, tags, HitFlags.None, multiplier);
            _extraCritChance = 0;
        }

        public BigNumber SkillDamage(double baseDamage, double multiplier, StatTags tags) =>
            _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * multiplier;

        public void NotePause(long idleTicksBefore, bool hasInput)
        {
            var conditions = _rules.Conditions;
            if (hasInput && idleTicksBefore * GameLoop.TickSeconds >= conditions.AfterIdlePauseSeconds)
                _state.AfterIdleTicks = (int)System.Math.Round(conditions.AfterIdleSeconds * GameLoop.TicksPerSecond);
            else if (_state.AfterIdleTicks > 0)
                _state.AfterIdleTicks--;
        }

        public double ConditionMultiplier(StatTags tags)
        {
            var conditions = _rules.Conditions;
            var multiplier = 1.0;
            if (!_state.FightingBoss)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageVsNormal, 1, tags));
            if (_state.FightingBoss)
            {
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageVsBoss, 1, tags));
                if (_state.EnemyType == EnemyType.Guardian)
                    multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageVsGuardian, 1, tags));
                if (_state.BossSecondsLeft <= conditions.BossTimerEndSeconds)
                    multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageAtBossTimerEnd, 1, tags));
            }
            if (_state.HitsOnEnemy < conditions.FirstHits)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.FirstHitsBonus, 0, tags));
            if (_state.HitsOnEnemy == 0)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.FrozenFirstHit, 0, tags));
            if (_state.KillStreak > 0)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.DamagePerKill, 0, tags)) * System.Math.Min(_state.KillStreak, conditions.KillStreakMax);
            if (_state.AfterIdleTicks > 0)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageAfterIdle, 1, tags));
            var perShot = _hero.Value(CombatStats.CannonShotDamage, 0, tags);
            if (perShot > 0 && _state.CannonShotsOnEnemy > 0)
                multiplier *= 1 + System.Math.Min(System.Math.Max(0, _hero.Value(CombatStats.CannonShotDamageMax, 0, tags)), perShot * _state.CannonShotsOnEnemy);
            var perTenPoison = _hero.Value(CombatStats.DamagePerTenPoison, 0, tags);
            if (perTenPoison > 0)
                multiplier *= 1 + perTenPoison * System.Math.Floor(_state.PoisonStacks / 10 + 1e-9);
            var perKind = _hero.Value(CombatStats.DamagePerInputKind, 0, tags);
            if (perKind > 0)
                multiplier *= 1 + perKind * RecentInputKinds();
            return multiplier * PathMultiplier(tags);
        }

        private double PathMultiplier(StatTags tags)
        {
            var multiplier = 1 + System.Math.Max(0, _hero.Value(CombatStats.DamagePerResource, 0, tags)) * _state.ResourceFill;
            var perCombo = _hero.Value(CombatStats.DamagePerCombo, 0, tags);
            if (perCombo > 0)
                multiplier *= 1 + System.Math.Min(System.Math.Max(0, _hero.Value(CombatStats.DamagePerComboMax, 0, tags)), perCombo * _state.Combo);
            var ramp = _hero.Value(CombatStats.RampPerSecond, 0, tags);
            if (ramp > 0 && _state.EnemyAlive)
                multiplier *= 1 + System.Math.Min(System.Math.Max(0, _hero.Value(CombatStats.RampMax, 0, tags)), ramp * _state.EnemyLifeSeconds);
            var balance = _hero.Value(CombatStats.DamageAtBalance, 0, tags);
            if (balance > 0)
                multiplier *= 1 + balance * Balance();
            if (_state.ActiveAuras > 0)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.DamagePerAura, 0, tags)) * _state.ActiveAuras;
            if (_state.VulnerabilityTicks > 0)
                multiplier *= 1 + _state.VulnerabilityShare;
            return multiplier;
        }

        public double Balance()
        {
            var keys = _state.KeyHitRate;
            var mouse = _state.MouseHitRate;
            var larger = System.Math.Max(keys, mouse);
            return larger <= 1e-9 ? 0 : System.Math.Min(keys, mouse) / larger;
        }

        public void DirectHit(BigNumber damage)
        {
            if (!_state.EnemyAlive || damage.Sign <= 0)
                return;
            _state.EnemyHp -= damage;
            LastTickDamage += damage;
            NoteDamage(damage);
            _events.Add(GameEvent.Damage(_state.Tick, damage, DamageSource.Skill, HitFlags.None));
            if (_state.EnemyAlive)
                return;
            _state.EnemyHp = BigNumber.Zero;
            _kills?.OnEnemyKilled();
        }

        public void Tick(in EffectiveInput input)
        {
            LastTickDamage = BigNumber.Zero;
            ArmSureCrits();
            if (_state.VulnerabilityTicks > 0 && --_state.VulnerabilityTicks == 0)
                _state.VulnerabilityShare = 0;
            CountStreakTime();
            var keyHits = TakeWhole(_state.KeyCarry + input.Keys, out var keyCarry);
            _state.KeyCarry = keyCarry;
            var mouseHits = TakeWhole((_state.MouseCarryPx + input.MousePx) / _rules.MousePxPerHit, out var mouseCarry);
            _state.MouseCarryPx = mouseCarry * _rules.MousePxPerHit;

            for (var i = 0; i < keyHits; i++)
            {
                var multiplier = _class.OnKeyHit(out var flags) * HeavyStrike(ref flags) * KeyBurst();
                Hit(DamageSource.Keyboard, _rules.KeyDamage, StatTags.Keyboard, flags, _state.FocusMultiplier * multiplier);
                KeyHitLightning();
                ShadowClone();
                QueueStrike();
            }
            for (var i = 0; i < mouseHits; i++)
                Hit(DamageSource.Mouse, _rules.MouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier);
            MouseBursts(input.MousePx);
            _class.EndTick(keyHits, input.MousePx);
            NoteRates(keyHits, input.MousePx / _rules.MousePxPerHit);
        }

        private void NoteRates(double keyHits, double mouseHits)
        {
            var blend = System.Math.Min(1, GameLoop.TickSeconds / _rules.Paths.BalanceWindowSeconds);
            _state.KeyHitRate += (keyHits * GameLoop.TicksPerSecond - _state.KeyHitRate) * blend;
            _state.MouseHitRate += (mouseHits * GameLoop.TicksPerSecond - _state.MouseHitRate) * blend;
        }

        private bool EveryNthKeyHit(StatId every)
        {
            var n = (long)System.Math.Round(_hero.Value(every, 0, StatTags.Keyboard));
            return n >= 1 && _state.KeyHits % n == 0;
        }

        private void ShadowClone()
        {
            if (!EveryNthKeyHit(CombatStats.CloneEvery))
                return;
            var multiplier = _hero.Value(CombatStats.CloneMultiplier, 0, StatTags.Keyboard);
            if (multiplier > 0)
                Hit(DamageSource.Skill, _rules.KeyDamage, StatTags.Keyboard, HitFlags.None, _state.FocusMultiplier * multiplier);
        }

        private void QueueStrike()
        {
            if (!EveryNthKeyHit(CombatStats.QueueStrikeEvery))
                return;
            var multiplier = _hero.Value(CombatStats.QueueStrikeMultiplier, 0, StatTags.Keyboard);
            if (multiplier > 0)
                _state.AddToQueue(SkillDamage(_rules.KeyDamage, _state.FocusMultiplier * multiplier, StatTags.Keyboard));
        }

        private void Hit(DamageSource source, double baseDamage, StatTags tags, HitFlags flags, double multiplier)
        {
            if (!_state.EnemyAlive)
                return;

            var supporting = source == DamageSource.Skill ? Supporting : null;
            if (supporting != null)
                multiplier *= SupportEffects.HitMultiplier(supporting, _state);
            if (source == DamageSource.Keyboard)
                multiplier *= _state.SupportKeyBonus;
            else if (source == DamageSource.Mouse)
                multiplier *= _state.SupportMouseBonus;
            LastHitCrit = false;
            var damage = _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * (multiplier * ConditionMultiplier(tags));
            if (damage.Sign <= 0)
                return;
            _state.HitsOnEnemy++;

            if (ForcedCrit() || ComboStrikeCrit(flags) || RollCrit(tags, supporting))
            {
                damage *= CritMultiplier(tags) * (supporting == null ? 1 : SupportEffects.CritMultiplier(supporting));
                flags |= HitFlags.Crit;
                LastHitCrit = true;
                _state.Crits++;
                if (source != DamageSource.Skill)
                    _keyHits?.OnCrit();
            }

            if (supporting != null && Redirected(supporting, damage))
                return;
            damage = WithholdDoom(damage);
            var hpBefore = _state.EnemyHp;
            _state.EnemyHp -= damage;
            NoteDamage(damage);
            LastTickDamage += damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, source, flags));
            if (source == DamageSource.Keyboard)
            {
                PierceQueue(damage);
                _keyHits?.OnKeyHit(damage);
            }
            if (supporting != null)
                SupportEffects.AfterHit(supporting, _state, damage, _rules.Dots.PoisonMax);
            else if (source == DamageSource.Mouse)
                _keyHits?.OnMouseHit(damage);
            if (_state.EnemyAlive && !Culled(tags))
                return;

            CarryOverkill(damage - hpBefore, tags);
            _state.EnemyHp = BigNumber.Zero;
            if (supporting != null)
                SupportEffects.OnKill(supporting, _state);
            _kills?.OnEnemyKilled();
        }

        private bool Redirected(SkillSlot supporting, BigNumber damage)
        {
            var redirect = SupportEffects.Redirect(supporting);
            if (redirect <= 0)
                return false;
            _state.QueueDamage[0] += damage * redirect;
            return true;
        }

        public const double MaxDoomShare = 0.95;

        private BigNumber WithholdDoom(BigNumber damage)
        {
            var share = System.Math.Min(MaxDoomShare, _hero.Value(CombatStats.DoomShare, 0));
            if (share <= 0)
                return damage;
            var withheld = damage * share;
            _state.Doom += withheld;
            if (_state.DoomTicks <= 0)
                _state.DoomTicks = System.Math.Max(1, (int)System.Math.Round((_rules.Paths.DoomSeconds + _hero.Value(CombatStats.DoomSeconds, 0)) * GameLoop.TicksPerSecond));
            return damage - withheld;
        }

        private bool ComboStrikeCrit(HitFlags flags) =>
            (flags & HitFlags.ComboStrike) != 0 && _hero.Value(CombatStats.ComboStrikeCrit, 0, StatTags.Keyboard) > 0;

        private void PierceQueue(BigNumber damage)
        {
            var share = System.Math.Min(1, _hero.Value(CombatStats.KeyHitQueueShare, 0, StatTags.Keyboard));
            if (share > 0)
                _state.QueueDamage[0] += damage * share;
            var all = System.Math.Min(1, _hero.Value(CombatStats.KeyHitQueueAll, 0, StatTags.Keyboard));
            if (all > 0)
                _state.AddToQueue(damage * all);
        }

        public int RecentInputKinds()
        {
            var window = (long)System.Math.Round(_rules.Conditions.InputKindSeconds * GameLoop.TicksPerSecond);
            return (_state.KeylessTicks < window ? 1 : 0) + (_state.MouseIdleTicks < window ? 1 : 0) + (_state.ClicklessTicks < window ? 1 : 0);
        }

        private bool Culled(StatTags tags)
        {
            var threshold = System.Math.Min(_rules.Conditions.CullMax, _hero.Value(CombatStats.CullThreshold, 0, tags));
            return threshold > 0 && _state.EnemyHp < _state.EnemyMaxHp * threshold;
        }

        private void CountStreakTime()
        {
            if (_state.KillStreak == 0)
                return;
            _state.TicksSinceKill++;
            if (_state.TicksSinceKill > _rules.Conditions.KillStreakResetSeconds * GameLoop.TicksPerSecond)
                _state.KillStreak = 0;
        }

        private void CarryOverkill(BigNumber overkill, StatTags tags)
        {
            var share = System.Math.Min(1, _hero.Value(CombatStats.OverkillCarry, 0, tags));
            if (share > 0 && overkill.Sign > 0)
                _state.QueueDamage[0] += overkill * share;
        }

        private void KeyHitLightning()
        {
            var share = _hero.Value(CombatStats.KeyHitLightning, 0, StatTags.Skill);
            if (share > 0)
                Hit(DamageSource.Skill, _rules.KeyDamage, StatTags.Skill, HitFlags.None, share);
        }

        private double KeyBurst()
        {
            var every = (long)System.Math.Round(_hero.Value(CombatStats.KeyBurstEvery, 0, StatTags.Keyboard));
            if (every < 1 || _state.KeyHits % every != 0)
                return 1;
            return System.Math.Max(1, _hero.Value(CombatStats.KeyBurstMultiplier, 0, StatTags.Keyboard));
        }

        private void MouseBursts(double mousePx)
        {
            var every = _hero.Value(CombatStats.MouseBurstPx, 0, StatTags.Mouse);
            if (every <= 0)
            {
                _state.MouseBurstCarryPx = 0;
                return;
            }
            _state.MouseBurstCarryPx += mousePx;
            var multiplier = System.Math.Max(1, _hero.Value(CombatStats.MouseBurstMultiplier, 0, StatTags.Mouse));
            while (_state.MouseBurstCarryPx >= every)
            {
                _state.MouseBurstCarryPx -= every;
                Hit(DamageSource.Mouse, _rules.MouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier * multiplier);
            }
        }

        private double HeavyStrike(ref HitFlags flags)
        {
            _state.KeyHits++;
            var every = (long)System.Math.Round(_hero.Value(CombatStats.KeyStrikeEvery, 0, StatTags.Keyboard));
            if (every < 1 || _state.KeyHits % every != 0)
                return 1;
            flags |= HitFlags.HeavyStrike;
            return System.Math.Max(1, _hero.Value(CombatStats.KeyStrikeMultiplier, 0, StatTags.Keyboard));
        }

        public double CritChance(StatTags tags)
        {
            var chance = _hero.Compute(CombatStats.CritChance, BigNumber.FromDouble(_rules.CritChance), tags | StatTags.Crit).ToDouble();
            if (_state.Doom.Sign > 0)
                chance *= System.Math.Max(0, _hero.Value(CombatStats.DoomedCritChance, 1));
            return System.Math.Min(1, System.Math.Max(0, chance));
        }

        public double CritMultiplier(StatTags tags) =>
            System.Math.Max(1, _hero.Compute(CombatStats.CritMultiplier, BigNumber.FromDouble(_rules.CritMultiplier), tags | StatTags.Crit).ToDouble());

        private void NoteDamage(BigNumber damage)
        {
            _state.TotalDamage += damage;
            if (_state.GoldenTicks > 0)
                _state.GoldenDamage += damage;
            if (_state.WishShare > 0)
                _state.WishStored += damage * _state.WishShare;
        }

        private void ArmSureCrits()
        {
            var sure = (int)System.Math.Round(System.Math.Max(0, _hero.Value(CombatStats.SureCrits, 0)));
            if (sure <= 0)
            {
                _state.SureCritsArmed = false;
                _state.SureCritsLeft = 0;
                return;
            }
            if (_state.SureCritsArmed)
                return;
            _state.SureCritsArmed = true;
            _state.SureCritsLeft = sure;
        }

        private bool ForcedCrit()
        {
            if (_state.SureCritsLeft > 0)
            {
                _state.SureCritsLeft--;
                return true;
            }
            var every = (long)System.Math.Round(_hero.Value(CombatStats.CritEvery, 0));
            if (every < 1)
            {
                _hitsTowardCrit = 0;
                return false;
            }
            return ++_hitsTowardCrit % every == 0;
        }

        private bool RollCrit(StatTags tags, SkillSlot? supporting)
        {
            var support = supporting == null ? 0 : SupportEffects.CritChance(supporting, _state);
            var chance = System.Math.Min(1, CritChance(tags) + _extraCritChance + support);
            return chance > 0 && _state.Rng.NextDouble() < chance;
        }

        private static int TakeWhole(double amount, out double remainder)
        {
            var whole = System.Math.Floor(amount + 1e-9);
            remainder = System.Math.Max(0, amount - whole);
            return (int)whole;
        }
    }
}
