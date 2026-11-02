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
        private readonly RunedancerMechanics _runedancer;
        private readonly DuelistMechanics _duelist;
        private readonly WindshotMechanics _windshot;
        private readonly ShardcallerMechanics _shardcaller;
        private readonly SandwalkerMechanics _sandwalker;
        private readonly MinstrelMechanics _minstrel;
        private readonly DreamcatcherMechanics _dreamcatcher;
        private readonly LanternbearerMechanics _lanternbearer;
        private readonly WhispererMechanics _whisperer;
        private readonly ClockwrightMechanics _clockwright;
        private readonly AlchemistMechanics _alchemist;
        private readonly DowserMechanics _dowser;
        private readonly SoulbinderMechanics _soulbinder;
        private readonly GhostcaptainMechanics _ghostcaptain;
        private readonly MothcallerMechanics _mothcaller;
        private readonly CartomancerMechanics _cartomancer;
        private readonly HerbwitchMechanics _herbwitch;
        private readonly GolemancerMechanics _golemancer;
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
            _runedancer = new RunedancerMechanics(rules.Classes.Runedancer, state, hero, this);
            _duelist = new DuelistMechanics(rules.Classes.Duelist, state, hero, this);
            _windshot = new WindshotMechanics(rules.Classes.Windshot, state, hero, this);
            _shardcaller = new ShardcallerMechanics(rules.Classes.Shardcaller, state, hero, this);
            _sandwalker = new SandwalkerMechanics(rules.Classes.Sandwalker, state, hero, this);
            _minstrel = new MinstrelMechanics(rules.Classes.Minstrel, state, hero, this);
            _dreamcatcher = new DreamcatcherMechanics(rules.Classes.Dreamcatcher, state, hero, this);
            _lanternbearer = new LanternbearerMechanics(rules.Classes.Lanternbearer, state, hero, this);
            _whisperer = new WhispererMechanics(rules.Classes.Whisperer, state, hero, this);
            _clockwright = new ClockwrightMechanics(rules.Classes.Clockwright, state, hero, this);
            _alchemist = new AlchemistMechanics(rules.Classes.Alchemist, state, hero, this);
            _dowser = new DowserMechanics(rules.Classes.Dowser, state, hero, this);
            _soulbinder = new SoulbinderMechanics(rules.Classes.Soulbinder, state, hero, this);
            _ghostcaptain = new GhostcaptainMechanics(rules.Classes.Ghostcaptain, state, hero, this);
            _mothcaller = new MothcallerMechanics(rules.Classes.Mothcaller, state, hero, this);
            _cartomancer = new CartomancerMechanics(rules.Classes.Cartomancer, state, hero, this);
            _herbwitch = new HerbwitchMechanics(rules.Classes.Herbwitch, state, hero, this);
            _golemancer = new GolemancerMechanics(rules.Classes.Golemancer, state, hero, this);
        }

        public ClassMechanics Class => _class;
        public RunedancerMechanics Runedancer => _runedancer;
        public DuelistMechanics Duelist => _duelist;
        public WindshotMechanics Windshot => _windshot;
        public ShardcallerMechanics Shardcaller => _shardcaller;
        public SandwalkerMechanics Sandwalker => _sandwalker;
        public MinstrelMechanics Minstrel => _minstrel;
        public DreamcatcherMechanics Dreamcatcher => _dreamcatcher;
        public LanternbearerMechanics Lanternbearer => _lanternbearer;
        public WhispererMechanics Whisperer => _whisperer;
        public ClockwrightMechanics Clockwright => _clockwright;
        public AlchemistMechanics Alchemist => _alchemist;
        public DowserMechanics Dowser => _dowser;
        public SoulbinderMechanics Soulbinder => _soulbinder;
        public GhostcaptainMechanics Ghostcaptain => _ghostcaptain;
        public MothcallerMechanics Mothcaller => _mothcaller;
        public CartomancerMechanics Cartomancer => _cartomancer;
        public HerbwitchMechanics Herbwitch => _herbwitch;
        public GolemancerMechanics Golemancer => _golemancer;
        public BigNumber LastTickDamage { get; private set; }

        public void UseRules(CombatRules rules)
        {
            _rules = rules;
            _class.UseRules(rules.Classes);
            _runedancer.UseRules(rules.Classes.Runedancer);
            _duelist.UseRules(rules.Classes.Duelist);
            _windshot.UseRules(rules.Classes.Windshot);
            _shardcaller.UseRules(rules.Classes.Shardcaller);
            _sandwalker.UseRules(rules.Classes.Sandwalker);
            _minstrel.UseRules(rules.Classes.Minstrel);
            _dreamcatcher.UseRules(rules.Classes.Dreamcatcher);
            _lanternbearer.UseRules(rules.Classes.Lanternbearer);
            _whisperer.UseRules(rules.Classes.Whisperer);
            _clockwright.UseRules(rules.Classes.Clockwright);
            _alchemist.UseRules(rules.Classes.Alchemist);
            _dowser.UseRules(rules.Classes.Dowser);
            _soulbinder.UseRules(rules.Classes.Soulbinder);
            _ghostcaptain.UseRules(rules.Classes.Ghostcaptain);
            _mothcaller.UseRules(rules.Classes.Mothcaller);
            _cartomancer.UseRules(rules.Classes.Cartomancer);
            _herbwitch.UseRules(rules.Classes.Herbwitch);
            _golemancer.UseRules(rules.Classes.Golemancer);
        }

        public void OnKill(IKillHandler kills) => _kills = kills;

        public void OnKeyHit(IKeyHitListener keyHits) => _keyHits = keyHits;

        public double BaseKeyDamage => _rules.KeyDamage;
        public CurseRules Curses => _rules.Curses;
        public ConditionRules Conditions => _rules.Conditions;
        public DotRules Dots => _rules.Dots;
        public MinionRules Skeletons => _rules.Skeletons;
        public MinionRules Beasts => _rules.Beasts;
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
            var streakPause = conditions.LongStreakPauseSeconds + System.Math.Max(0, _hero.Value(CombatStats.LongStreakPauseSeconds, 0));
            var paused = !hasInput && (idleTicksBefore + 1) * GameLoop.TickSeconds >= streakPause;
            _state.InputStreakTicks = paused ? 0 : _state.InputStreakTicks + 1;
            if (hasInput && idleTicksBefore * GameLoop.TickSeconds >= conditions.AfterIdlePauseSeconds)
            {
                _state.AfterIdleTicks = (int)System.Math.Round(conditions.AfterIdleSeconds * GameLoop.TicksPerSecond);
                _state.PauseCritArmed = _hero.Value(CombatStats.PauseCrit, 0) > 0;
            }
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
            var longStreak = System.Math.Max(0, _hero.Value(CombatStats.DamageLongStreak, 1, tags));
            if (_state.InputStreakTicks * GameLoop.TickSeconds >= conditions.LongStreakSeconds)
                multiplier *= longStreak;
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
            if (_state.CompanionMarks > 0)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.CompanionPerMark, 0, tags)) * _state.CompanionMarks;
            if (_state.BlindTicks > 0)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageVsBlinded, 1, tags));
            if (_state.VulnerabilityTicks > 0 && !_state.SharedEnemy)
                multiplier *= 1 + _state.VulnerabilityShare;
            return multiplier * _runedancer.SkillMultiplier(tags);
        }

        public double Balance()
        {
            var keys = _state.KeyHitRate;
            var mouse = _state.MouseHitRate;
            var larger = System.Math.Max(keys, mouse);
            return larger <= 1e-9 ? 0 : System.Math.Min(keys, mouse) / larger;
        }

        public void ExtraStrike()
        {
            _runedancer.AfterStrike(false);
            _duelist.AfterStrike(false);
            _windshot.AfterStrike(false);
            _shardcaller.AfterStrike(false);
            _sandwalker.AfterStrike();
            _minstrel.AfterStrike();
            _dreamcatcher.AfterStrike();
            _lanternbearer.AfterStrike(false);
            _whisperer.AfterStrike();
            _clockwright.AfterStrike();
            _alchemist.AfterStrike(false);
            _dowser.AfterStrike();
            _soulbinder.AfterStrike();
            _ghostcaptain.AfterStrike();
            _mothcaller.AfterStrike(false);
            _cartomancer.AfterStrike();
            _herbwitch.AfterStrike();
            _golemancer.AfterStrike();
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
            _runedancer.Tick();
            _duelist.Tick();
            _windshot.Tick(input.MousePx);
            _shardcaller.Tick();
            _sandwalker.Tick(input.MousePx);
            _minstrel.Tick();
            _dreamcatcher.Tick(input.MousePx);
            _lanternbearer.Tick();
            _whisperer.Tick();
            _clockwright.Tick();
            _alchemist.Tick();
            _dowser.Tick();
            _soulbinder.Tick();
            _ghostcaptain.Tick();
            _mothcaller.Tick();
            _cartomancer.Tick();
            _herbwitch.Tick();
            _golemancer.Tick();
            var keyHits = TakeWhole(_state.KeyCarry + input.Hits, out var keyCarry);
            _state.KeyCarry = keyCarry;
            var mouseHits = TakeWhole((_state.MouseCarryPx + input.MousePx) / _rules.MousePxPerHit, out var mouseCarry);
            _state.MouseCarryPx = mouseCarry * _rules.MousePxPerHit;

            for (var i = 0; i < keyHits; i++)
            {
                var multiplier = _class.OnKeyHit(out var flags) * HeavyStrike(ref flags) * KeyBurst() * _runedancer.StrikeMultiplier() * _duelist.StrikeMultiplier() * _windshot.StrikeMultiplier() * _minstrel.StrikeMultiplier() * _whisperer.StrikeMultiplier() * _clockwright.StrikeMultiplier() * _alchemist.StrikeMultiplier() * _dowser.StrikeMultiplier() * _mothcaller.StrikeMultiplier();
                var alive = _state.EnemyAlive;
                Hit(DamageSource.Keyboard, _rules.KeyDamage, StatTags.Keyboard, flags, _state.FocusMultiplier * multiplier);
                if (alive)
                {
                    var crit = LastHitCrit;
                    _runedancer.AfterStrike(crit);
                    _duelist.AfterStrike(crit);
                    _windshot.AfterStrike(crit);
                    _shardcaller.AfterStrike(crit);
                    _sandwalker.AfterStrike();
                    _minstrel.AfterStrike();
                    _dreamcatcher.AfterStrike();
                    _lanternbearer.AfterStrike(crit);
                    _whisperer.AfterStrike();
                    _clockwright.AfterStrike();
                    _alchemist.AfterStrike(crit);
                    _dowser.AfterStrike();
                    _soulbinder.AfterStrike();
                    _ghostcaptain.AfterStrike();
                    _mothcaller.AfterStrike(crit);
                    _cartomancer.AfterStrike();
                    _herbwitch.AfterStrike();
                    _golemancer.AfterStrike();
                }
                KeyHitLightning();
                ShadowClone();
                QueueStrike();
            }
            for (var i = 0; i < mouseHits; i++)
                Hit(DamageSource.Mouse, _rules.MouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier * _whisperer.StrikeMultiplier());
            MouseBursts(input.MousePx);
            _class.EndTick(keyHits, input.MousePx);
            _runedancer.EndTick();
            _duelist.EndTick();
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
            var damage = _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * (multiplier * ConditionMultiplier(tags) * StunnedMultiplier(tags) * MarkedMultiplier(tags) * _shardcaller.DamageTaken() * _sandwalker.DamageTaken() * _minstrel.DamageTaken(source) * _dreamcatcher.DamageTaken() * _lanternbearer.DamageTaken() * _whisperer.DamageTaken() * _alchemist.DamageTaken() * _soulbinder.DamageTaken() * _ghostcaptain.DamageTaken() * _cartomancer.DamageTaken());
            if (damage.Sign <= 0)
                return;
            _state.HitsOnEnemy++;

            if (ForcedCrit() || _windshot.ForcesCrit(tags) || _dowser.ForcesCrit || RollCrit(tags, supporting))
            {
                damage *= System.Math.Max(1, CritMultiplier(tags) * _windshot.CritScale(tags)) * (supporting == null ? 1 : SupportEffects.CritMultiplier(supporting)) * CritBurst();
                flags |= HitFlags.Crit;
                LastHitCrit = true;
                _state.Crits++;
                if (source != DamageSource.Skill)
                    _keyHits?.OnCrit();
                if (source == DamageSource.Keyboard)
                    _state.PendingResource += System.Math.Max(0, _hero.Value(CombatStats.ResourcePerCrit, 0, StatTags.Keyboard));
            }

            if (supporting != null && Redirected(supporting, damage))
                return;
            if (_minstrel.Dream(damage))
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
            if (source == DamageSource.Mouse)
                PierceQueueWithMouse(damage);
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

        private double StunnedMultiplier(StatTags tags) =>
            _state.StunTicks > 0 ? System.Math.Max(0, _hero.Value(CombatStats.DamageVsStunned, 1, tags)) : 1;

        public double MarkedMultiplier(StatTags tags)
        {
            if (_state.HunterMarkTicks <= 0)
                return 1;
            var bonus = ((tags & StatTags.Keyboard) != 0 ? _rules.Classes.HunterMark.KeyDamage : 0) + System.Math.Max(0, _hero.Value(CombatStats.MarkDamage, 0, tags));
            return 1 + bonus * (1 + System.Math.Max(0, _hero.Value(CombatStats.MarkPower, 0)));
        }

        private void PierceQueue(BigNumber damage)
        {
            var share = System.Math.Min(1, _hero.Value(CombatStats.KeyHitQueueShare, 0, StatTags.Keyboard));
            if (share > 0)
                _state.QueueDamage[0] += damage * share;
            var all = System.Math.Min(1, _hero.Value(CombatStats.KeyHitQueueAll, 0, StatTags.Keyboard));
            if (all > 0)
                _state.AddToQueue(damage * all);
        }

        private void PierceQueueWithMouse(BigNumber damage)
        {
            var share = System.Math.Min(1, _hero.Value(CombatStats.MouseQueueShare, 0, StatTags.Mouse));
            if (share > 0)
                _state.QueueDamage[0] += damage * share;
        }

        private double CritBurst()
        {
            var every = (long)System.Math.Round(_hero.Value(CombatStats.CritBurstEvery, 0));
            if (every < 1)
                return 1;
            return ++_state.CritsTowardBurst % every == 0 ? System.Math.Max(1, _hero.Value(CombatStats.CritBurstMultiplier, 0)) : 1;
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

        public double CritChance(StatTags tags) => System.Math.Min(1, System.Math.Max(0, RawCritChance(tags)));

        public double Overcrit(StatTags tags) => System.Math.Max(0, RawCritChance(tags) - 1);

        private double RawCritChance(StatTags tags)
        {
            var chance = _hero.Compute(CombatStats.CritChance, BigNumber.FromDouble(_rules.CritChance), tags | StatTags.Crit).ToDouble();
            if (_state.Doom.Sign > 0)
                chance *= System.Math.Max(0, _hero.Value(CombatStats.DoomedCritChance, 1));
            return chance;
        }

        public double CritMultiplier(StatTags tags) =>
            System.Math.Max(1, _hero.Compute(CombatStats.CritMultiplier, BigNumber.FromDouble(_rules.CritMultiplier), tags | StatTags.Crit).ToDouble())
            * (1 + Overcrit(tags) * _rules.OvercritRate);

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
            if (_state.PauseCritArmed)
            {
                _state.PauseCritArmed = false;
                return true;
            }
            if (_state.HitsOnEnemy == 1 && _hero.Value(CombatStats.FirstHitCrit, 0) > 0)
                return true;
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
