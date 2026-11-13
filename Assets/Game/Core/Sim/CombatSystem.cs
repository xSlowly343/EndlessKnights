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

        public double BaseKeyDamage => _rules.StrikeDamage * AttackScale * Additional;
        public double BaseMouseDamage => _rules.MouseDamage * AttackScale * Additional;
        public double Additional { get; private set; } = 1;
        public double Attack { get; private set; }
        public double AttackScale { get; private set; } = 1;
        public double WeaponDamage { get; private set; }
        public double AttributeTotal { get; private set; }
        public double FlatAttack { get; private set; }

        public double ItemAttribute()
        {
            var total = 0.0;
            var worn = _state.Gear.Worn;
            for (var i = 0; i < worn.Length; i++)
            {
                var item = worn[i];
                if (item != null && item.Wearable)
                    total += _rules.Attack.Attribute(item.Rarity, item.ItemLevel) * _rules.Mount.Factor(_state.Mount.Levels[i + 1]);
            }
            return total;
        }

        public void RefreshAttack(double classPoints)
        {
            var rules = _rules.Attack;
            var held = _state.Weapons.Held;
            var weapon = ((held == null ? rules.BaseWeaponDamage : rules.WeaponDamage(held.Rarity, held.ItemLevel)) + System.Math.Max(0, _hero.Value(CombatStats.WeaponDamage, 0))) * _rules.Mount.Factor(_state.Mount.Levels[MountSystem.WeaponSlot]);
            var attribute = rules.BaseAttribute + System.Math.Max(0, classPoints) + ItemAttribute() + System.Math.Max(0, _hero.Value(CombatStats.ItemAttribute, 0));
            WeaponDamage = weapon;
            AttributeTotal = attribute;
            FlatAttack = _hero.Value(CombatStats.FlatAttack, 0);
            Attack = rules.Attack(attribute, weapon, FlatAttack);
            AttackScale = rules.BaseAttack > 0 ? Attack / rules.BaseAttack : 1;
            Additional = 1 + System.Math.Max(0, _hero.Value(CombatStats.AdditionalDamage, 0)) + (held == null ? 0 : rules.QualityBonus(held.Quality));
        }
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
            _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * (multiplier * _state.DamageScale);

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
            if (!hasInput)
                Rest(idleTicksBefore + 1);
        }

        public double TempoBonus
        {
            get
            {
                var tempo = _hero.Value(CombatStats.TempoDamage, 0);
                if (!(tempo > 0))
                    return 0;
                var rules = _rules.Tempo;
                return System.Math.Min(rules.MaxBonus, tempo * rules.Steps(_state.TempoPerMinute));
            }
        }

        public double RestChargeMax => System.Math.Max(0, _hero.Value(CombatStats.RestCharge, 0)) * _rules.Tempo.RestMaxMinutes;

        private void Rest(long idleTicks)
        {
            var charge = _hero.Value(CombatStats.RestCharge, 0);
            if (!(charge > 0) || idleTicks * GameLoop.TickSeconds <= _rules.Tempo.RestAfterSeconds)
                return;
            _state.RestCharge = System.Math.Min(RestChargeMax, _state.RestCharge + charge * GameLoop.TickSeconds / 60);
            _state.RestHitsLeft = _rules.Tempo.RestHits;
        }

        private double RestStrike()
        {
            if (_state.RestHitsLeft <= 0 || !(_state.RestCharge > 0))
                return 1;
            var multiplier = 1 + _state.RestCharge;
            if (--_state.RestHitsLeft == 0)
                _state.RestCharge = 0;
            return multiplier;
        }

        private void NoteTempo(double keyHits)
        {
            var blend = System.Math.Min(1, GameLoop.TickSeconds / _rules.Tempo.WindowSeconds);
            _state.TempoPerMinute += (keyHits * GameLoop.TicksPerSecond * 60 - _state.TempoPerMinute) * blend;
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
            var perTemper = _hero.Value(CombatStats.DamagePerTemperPercent, 0, tags);
            if (perTemper > 0)
                multiplier *= 1 + perTemper * _state.WornTemperPercent;
            var perCollection = _hero.Value(CombatStats.DamagePerMuseumCollection, 0, tags);
            if (perCollection > 0)
                multiplier *= 1 + perCollection * _state.Museum.Done.Count;
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
            FractureFade();
            Embers();
            ClearTraitParty();
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
                var multiplier = _class.OnKeyHit(out var flags) * HeavyStrike(ref flags) * KeyBurst() * RestStrike() * _runedancer.StrikeMultiplier() * _duelist.StrikeMultiplier() * _windshot.StrikeMultiplier() * _minstrel.StrikeMultiplier() * _whisperer.StrikeMultiplier() * _clockwright.StrikeMultiplier() * _alchemist.StrikeMultiplier() * _dowser.StrikeMultiplier() * _mothcaller.StrikeMultiplier();
                var alive = _state.EnemyAlive;
                Hit(DamageSource.Strike, BaseKeyDamage, StatTags.Strike, flags, _state.FocusMultiplier * multiplier);
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
                Hit(DamageSource.Mouse, BaseMouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier * _whisperer.StrikeMultiplier());
            MouseBursts(input.MousePx);
            ShareFracture();
            ShareShred();
            _class.EndTick(keyHits, input.MousePx);
            _runedancer.EndTick();
            _duelist.EndTick();
            NoteRates(keyHits, input.MousePx / _rules.MousePxPerHit);
            NoteTempo(keyHits);
        }

        private void NoteRates(double keyHits, double mouseHits)
        {
            var blend = System.Math.Min(1, GameLoop.TickSeconds / _rules.Paths.BalanceWindowSeconds);
            _state.KeyHitRate += (keyHits * GameLoop.TicksPerSecond - _state.KeyHitRate) * blend;
            _state.MouseHitRate += (mouseHits * GameLoop.TicksPerSecond - _state.MouseHitRate) * blend;
        }

        private bool EveryNthKeyHit(StatId every)
        {
            var n = (long)System.Math.Round(_hero.Value(every, 0, StatTags.Strike));
            return n >= 1 && _state.StrikeHits % n == 0;
        }

        private void ShadowClone()
        {
            if (!EveryNthKeyHit(CombatStats.CloneEvery))
                return;
            var multiplier = _hero.Value(CombatStats.CloneMultiplier, 0, StatTags.Strike);
            if (multiplier > 0)
                Hit(DamageSource.Skill, BaseKeyDamage, StatTags.Strike, HitFlags.None, _state.FocusMultiplier * multiplier);
        }

        private void QueueStrike()
        {
            if (!EveryNthKeyHit(CombatStats.QueueStrikeEvery))
                return;
            var multiplier = _hero.Value(CombatStats.QueueStrikeMultiplier, 0, StatTags.Strike);
            if (multiplier > 0)
                _state.AddToQueue(SkillDamage(BaseKeyDamage, _state.FocusMultiplier * multiplier, StatTags.Strike));
        }

        private void Hit(DamageSource source, double baseDamage, StatTags tags, HitFlags flags, double multiplier)
        {
            if (!_state.EnemyAlive)
                return;

            var supporting = source == DamageSource.Skill ? Supporting : null;
            if (supporting != null)
                multiplier *= SupportEffects.HitMultiplier(supporting, _state);
            if (source == DamageSource.Strike)
                multiplier *= _state.SupportKeyBonus;
            LastHitCrit = false;
            var damage = _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * (multiplier * _state.DamageScale * ConditionMultiplier(tags) * StunnedMultiplier(tags) * MarkedMultiplier(tags) * _shardcaller.DamageTaken() * _sandwalker.DamageTaken() * _minstrel.DamageTaken(source) * _dreamcatcher.DamageTaken() * _lanternbearer.DamageTaken() * _whisperer.DamageTaken() * _alchemist.DamageTaken() * _soulbinder.DamageTaken() * _ghostcaptain.DamageTaken() * _cartomancer.DamageTaken() * FractureTaken() * PillarMultiplier() * DefenseGain(tags) * ElementMultiplier(tags) * (1 + TempoBonus));
            if (damage.Sign <= 0)
                return;
            _state.HitsOnEnemy++;

            _critRoll = -1;
            if (ForcedCrit() || _windshot.ForcesCrit(tags) || _dowser.ForcesCrit || RollCrit(tags, supporting))
            {
                damage *= System.Math.Max(1, CritMultiplier(tags) * _windshot.CritScale(tags)) * (supporting == null ? 1 : SupportEffects.CritMultiplier(supporting, _state)) * CritBurst();
                flags |= HitFlags.Crit;
                if (RollOvercrit(tags))
                {
                    damage *= System.Math.Max(1, OvercritMultiplier(tags));
                    flags |= HitFlags.Overcrit;
                    _state.Overcrits++;
                }
                LastHitCrit = true;
                _state.Crits++;
                if (source != DamageSource.Skill)
                    _keyHits?.OnCrit();
                if (source == DamageSource.Strike)
                    _state.PendingResource += System.Math.Max(0, _hero.Value(CombatStats.ResourcePerCrit, 0, StatTags.Strike));
            }

            if (RollDoubleHit(tags))
                damage *= 2;
            if (RollDischarge())
                damage *= 1 + _rules.Elements.LightningPower;

            if (supporting != null && Redirected(supporting, damage))
                return;
            if (_minstrel.Dream(damage))
                return;
            damage = WithholdDoom(damage);
            var hpBefore = _state.EnemyHp;
            _state.EnemyHp -= damage;
            LayFracture(tags);
            ElementAfterHit(damage);
            NoteDamage(damage);
            LastTickDamage += damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, source, flags));
            Splash(damage, tags);
            if (source == DamageSource.Strike)
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
            Pierce(damage - hpBefore);
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
                _state.DoomTicks = System.Math.Max(1, (int)System.Math.Round(_rules.Paths.DoomSeconds * GameLoop.TicksPerSecond));
            return damage - withheld;
        }

        private double StunnedMultiplier(StatTags tags) =>
            _state.StunTicks > 0 ? System.Math.Max(0, _hero.Value(CombatStats.DamageVsStunned, 1, tags)) : 1;

        public double MarkedMultiplier(StatTags tags)
        {
            if (_state.HunterMarkTicks <= 0)
                return 1;
            var bonus = ((tags & StatTags.Strike) != 0 ? _rules.Classes.HunterMark.StrikeDamage : 0) + System.Math.Max(0, _hero.Value(CombatStats.MarkDamage, 0, tags));
            return 1 + bonus * (1 + System.Math.Max(0, _hero.Value(CombatStats.MarkPower, 0)));
        }

        private void PierceQueue(BigNumber damage)
        {
            var share = System.Math.Min(1, _hero.Value(CombatStats.KeyHitQueueShare, 0, StatTags.Strike));
            if (share > 0)
                _state.QueueDamage[0] += damage * share;
            var all = System.Math.Min(1, _hero.Value(CombatStats.KeyHitQueueAll, 0, StatTags.Strike));
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

        private void Pierce(BigNumber overkill)
        {
            if (overkill.Sign <= 0)
                return;
            var share = _state.EnemyType == EnemyType.Normal ? PierceShare : StageCarryShare;
            if (share > 0)
                _state.PierceDamage += overkill * share;
        }

        public double PierceShare => System.Math.Min(_rules.Pierce.MaxShare, _rules.BasePierce + System.Math.Max(0, _hero.Value(CombatStats.PierceShare, 0)));

        public double StageCarryShare => System.Math.Min(_rules.Pierce.MaxStageCarry, System.Math.Max(0, _hero.Value(CombatStats.StageCarry, 0)));

        private void Splash(BigNumber damage, StatTags tags)
        {
            if (_state.EnemyType != EnemyType.Normal)
                return;
            var share = System.Math.Min(_rules.Pierce.MaxSplashShare, _hero.Value(CombatStats.SplashShare, 0, tags));
            if (!(share > 0))
                return;
            var targets = System.Math.Min(_rules.Pierce.MaxSplashTargets, (int)System.Math.Floor(_hero.Value(CombatStats.SplashTargets, 0) + 1e-9));
            var queue = _state.QueueDamage;
            for (var i = 0; i < targets && i < queue.Length; i++)
                queue[i] += damage * share;
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
                Hit(DamageSource.Skill, BaseKeyDamage, StatTags.Skill, HitFlags.None, share);
        }

        private double KeyBurst()
        {
            var every = (long)System.Math.Round(_hero.Value(CombatStats.KeyBurstEvery, 0, StatTags.Strike));
            if (every < 1 || _state.StrikeHits % every != 0)
                return 1;
            return System.Math.Max(1, _hero.Value(CombatStats.KeyBurstMultiplier, 0, StatTags.Strike));
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
                Hit(DamageSource.Mouse, BaseMouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier * multiplier);
            }
        }

        private double HeavyStrike(ref HitFlags flags)
        {
            _state.StrikeHits++;
            var every = (long)System.Math.Round(_hero.Value(CombatStats.KeyStrikeEvery, 0, StatTags.Strike));
            if (every < 1 || _state.StrikeHits % every != 0)
                return 1;
            flags |= HitFlags.HeavyStrike;
            return System.Math.Max(1, _hero.Value(CombatStats.KeyStrikeMultiplier, 0, StatTags.Strike));
        }

        public double CritChance(StatTags tags) => System.Math.Min(1, System.Math.Max(0, RawCritChance(tags)));

        public double Overcrit(StatTags tags) => System.Math.Min(1, System.Math.Max(0, RawCritChance(tags) - 1));

        public double OvercritMultiplier(StatTags tags) =>
            CritMultiplier(tags) * System.Math.Max(0, 1 + _hero.Value(CombatStats.OvercritMultiplier, 0, tags | StatTags.Crit));

        private double RawCritChance(StatTags tags)
        {
            var chance = _hero.Compute(CombatStats.CritChance, BigNumber.FromDouble(_rules.CritChance), tags | StatTags.Crit).ToDouble();
            if (_state.Doom.Sign > 0)
                chance *= System.Math.Max(0, _hero.Value(CombatStats.DoomedCritChance, 1));
            return chance;
        }

        public double CritMultiplier(StatTags tags) =>
            System.Math.Max(1, _hero.Compute(CombatStats.CritMultiplier, BigNumber.FromDouble(_rules.CritMultiplier), tags | StatTags.Crit).ToDouble());

        private void NoteDamage(BigNumber damage)
        {
            _state.TotalDamage += damage;
            if (_state.GoldenTicks > 0)
                _state.GoldenDamage += damage;
            if (_state.Oddities.Kind == OddityKind.Dummy)
                _state.Oddities.DummyDamage += damage;
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

        private void ClearTraitParty()
        {
            _state.TraitPartyDamage = 0;
            _state.TraitPartyCritChance = 0;
            _state.TraitPartyCritMultiplier = 0;
            _state.TraitPartyGold = 0;
            _state.TraitPartyCooldown = 0;
            _state.TraitShareDamage = 0;
            _state.TraitShareCritChance = 0;
            _state.TraitShareCritMultiplier = 0;
            _state.TraitShareGold = 0;
        }

        public static readonly int FractureEffectId = Content.Gems.GemDef.NetIdOf("fracture");

        public int FractureMax => _rules.Fracture.MaxStacks(_hero.Value(CombatStats.FractureMax, 0));

        public double FractureChance(StatTags tags) => System.Math.Min(1, System.Math.Max(0, _hero.Value(CombatStats.FractureChance, 0, tags)));

        public double FracturePerStack => _rules.Fracture.PerStack * (1 + System.Math.Max(0, _hero.Value(CombatStats.FractureEffect, 0)));

        public double FractureBonus => _state.FractureStacks * FracturePerStack;

        public double Giving => PartyRole.Giving(_hero);

        public double Receiving => PartyRole.Receiving(_hero);

        public double PillarBonus => _state.PartyMembers > 1
            ? System.Math.Max(0, _hero.Value(CombatStats.Pillar, 0)) * System.Math.Min(_rules.Fracture.PillarMembers, _state.PartyMembers)
            : 0;

        public double PillarMultiplier() => 1 + PillarBonus;

        public double FractureTaken() => _state.SharedEnemy || _state.FractureStacks == 0 ? 1 : 1 + FractureBonus;

        private void LayFracture(StatTags tags)
        {
            _state.FractureQuietTicks = 0;
            var chance = System.Math.Min(1, FractureChance(tags) + _rules.Elements.CorrosionChance * _portion[(int)Element.Corrosion]);
            if (!(chance > 0) || _state.FractureStacks >= FractureMax)
                return;
            if (chance < 1 && _state.Rng.NextDouble() >= chance)
                return;
            _state.FractureStacks++;
            _state.FracturesLaid++;
            _fractureShareTicks = 0;
        }

        private void FractureFade()
        {
            if (_state.FractureStacks > FractureMax)
                _state.FractureStacks = FractureMax;
            if (_state.FractureStacks == 0)
                return;
            if (++_state.FractureQuietTicks < (int)System.Math.Round(_rules.Fracture.DecaySeconds * GameLoop.TicksPerSecond))
                return;
            _state.FractureQuietTicks = 0;
            _state.FractureStacks--;
        }

        private int _fractureShareTicks;

        private void ShareFracture()
        {
            if (!_state.SharedEnemy || _state.FractureStacks == 0)
            {
                _fractureShareTicks = 0;
                return;
            }
            if (_fractureShareTicks-- > 0)
                return;
            _fractureShareTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(FractureEffectId, FractureBonus * Giving, _rules.Fracture.ShareSeconds);
        }

        public DefenseRules Defense => _rules.Defense;

        public double ArmorPen(StatTags tags) => System.Math.Min(_rules.Defense.ArmorPenCap, System.Math.Max(0, _hero.Value(CombatStats.ArmorPen, 0, tags)));

        public double DefenseShred => System.Math.Min(_rules.Defense.ShredCap, System.Math.Max(0, _hero.Value(CombatStats.DefenseShred, 0)) + _state.EarthShred);

        public double DefenseGain(StatTags tags)
        {
            var defense = _state.EnemyDefense;
            if (!(defense > 0))
                return 1;
            var pen = ArmorPen(tags);
            var shred = _state.SharedEnemy ? 0 : DefenseShred;
            return pen > 0 || shred > 0 ? _rules.Defense.Gain(defense, shred, pen) : 1;
        }

        public double SharedShredGain => _rules.Defense.Gain(_state.EnemyDefense, System.Math.Min(_rules.Defense.ShredCap, DefenseShred * Giving), 0) - 1;

        private int _shredShareTicks;

        private void ShareShred()
        {
            if (!_state.SharedEnemy || !(_state.EnemyDefense > 0) || !(DefenseShred > 0))
            {
                _shredShareTicks = 0;
                return;
            }
            if (_shredShareTicks-- > 0)
                return;
            _shredShareTicks = GameLoop.TicksPerSecond - 1;
            var gain = SharedShredGain;
            if (gain > 0)
                _state.TraitEnemyEffects.Add(ShredEffectId(_state.Class), gain, _rules.Fracture.ShareSeconds);
        }

        private static readonly int[] ShredEffectIds = ShredIds();

        private static int ShredEffectId(HeroClass heroClass) => ShredEffectIds[(int)heroClass];

        private static int[] ShredIds()
        {
            var classes = System.Enum.GetValues(typeof(HeroClass));
            var ids = new int[classes.Length];
            foreach (HeroClass heroClass in classes)
                ids[(int)heroClass] = Content.Gems.GemDef.NetIdOf("shred_" + heroClass);
            return ids;
        }

        private readonly double[] _portion = new double[ElementRules.Count];

        public ElementRules Elements => _rules.Elements;

        public double Portion(Element element) => _portion[(int)element];

        public int ClassElement { get; set; } = ElementRules.None;

        public double ElementMultiplier(StatTags tags) => ElementMultiplier(tags, _portion);

        public double ElementFactor(StatTags tags) => ElementMultiplier(tags, _sheetPortion);

        private readonly double[] _sheetPortion = new double[ElementRules.Count];

        private double ElementMultiplier(StatTags tags, double[] portion)
        {
            var rules = _rules.Elements;
            var total = 0.0;
            for (var i = 0; i < ElementRules.Count; i++)
            {
                portion[i] = System.Math.Max(0, _hero.Value(ElementRules.Conversion[i], 0, tags) + ClassShare(i, CombatStats.ClassElementConversion, tags));
                total += portion[i];
            }
            if (!(total > 0))
                return 1;
            var scale = total > 1 ? 1 / total : 1;
            var multiplier = 1 - total * scale;
            for (var i = 0; i < ElementRules.Count; i++)
            {
                if (!(portion[i] > 0))
                    continue;
                var resist = System.Math.Max(rules.MinResist, rules.Resistance(i, _state.EnemyResists, _state.EnemyWeakTo) - System.Math.Max(0, _hero.Value(ElementRules.Pen[i], 0, tags) + ClassShare(i, CombatStats.ClassElementPen, tags)));
                portion[i] *= scale * (_hero.Value(ElementRules.Damage[i], 1, tags) + ClassShare(i, CombatStats.ClassElementDamage, tags)) * (1 - resist);
                multiplier += portion[i];
            }
            if (!(multiplier > 0))
                return 0;
            for (var i = 0; i < ElementRules.Count; i++)
                portion[i] /= multiplier;
            return multiplier;
        }

        private double ClassShare(int element, StatId stat, StatTags tags) =>
            element == ClassElement ? System.Math.Max(0, _hero.Value(stat, 0, tags)) : 0;

        private bool RollDischarge()
        {
            var chance = _rules.Elements.LightningChance * _portion[(int)Element.Lightning];
            if (!(chance > 0))
                return false;
            if (_state.Rng.NextDouble() >= chance)
                return false;
            _state.Discharges++;
            return true;
        }

        private void ElementAfterHit(BigNumber damage)
        {
            var rules = _rules.Elements;
            var fire = _portion[(int)Element.Fire];
            if (fire > 0 && rules.BurnShare > 0)
            {
                _state.EmberPool += damage * (fire * rules.BurnShare);
                _state.EmberTicks = System.Math.Max(1, (int)System.Math.Round(rules.BurnSeconds * GameLoop.TicksPerSecond));
            }
            var air = _portion[(int)Element.Air];
            if (air > 0 && rules.AirShare > 0 && _state.EnemyType == EnemyType.Normal)
                _state.QueueDamage[0] += damage * (air * rules.AirShare);
            var earth = _portion[(int)Element.Earth];
            if (earth > 0)
                _state.EarthShred = System.Math.Min(rules.EarthMax, _state.EarthShred + rules.EarthShred * earth);
            var water = _portion[(int)Element.Water];
            if (water > 0 && _state.FightingBoss && _state.BossTicksLeft > 0)
                AddWater(System.Math.Min(rules.WaterMax - _state.WaterSeconds, rules.WaterSeconds * water));
            System.Array.Clear(_portion, 0, _portion.Length);
        }

        private void AddWater(double seconds)
        {
            if (!(seconds > 0))
                return;
            _state.WaterSeconds += seconds;
            _state.WaterCarry += seconds * GameLoop.TicksPerSecond;
            var whole = (int)System.Math.Floor(_state.WaterCarry + 1e-9);
            if (whole <= 0)
                return;
            _state.WaterCarry -= whole;
            _state.BossTicksLeft += whole;
        }

        private void Embers()
        {
            if (_state.EmberTicks <= 0)
                return;
            if (!_state.EnemyAlive)
            {
                _state.EmberTicks = 0;
                _state.EmberPool = BigNumber.Zero;
                return;
            }
            var burn = _state.EmberPool * (1.0 / _state.EmberTicks);
            _state.EmberPool -= burn;
            _state.EmberTicks--;
            DirectHit(burn);
        }

        private bool RollDoubleHit(StatTags tags)
        {
            var chance = System.Math.Min(1, _hero.Value(CombatStats.DoubleHitChance, 0, tags));
            return chance > 0 && _state.Rng.NextDouble() < chance;
        }

        private bool RollCrit(StatTags tags, SkillSlot? supporting)
        {
            var support = supporting == null ? 0 : SupportEffects.CritChance(supporting, _state);
            var chance = System.Math.Min(1, CritChance(tags) + _extraCritChance + support);
            if (chance <= 0)
                return false;
            _critRoll = _state.Rng.NextDouble();
            return _critRoll < chance;
        }

        private double _critRoll = -1;

        private bool RollOvercrit(StatTags tags)
        {
            var chance = Overcrit(tags);
            if (chance <= 0)
                return false;
            var roll = _critRoll >= 0 ? _critRoll : _state.Rng.NextDouble();
            return roll < chance;
        }

        private static int TakeWhole(double amount, out double remainder)
        {
            var whole = System.Math.Floor(amount + 1e-9);
            remainder = System.Math.Max(0, amount - whole);
            return (int)whole;
        }
    }
}
