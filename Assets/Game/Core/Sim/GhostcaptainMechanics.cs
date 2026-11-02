using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GhostcaptainMechanics : IKillRewardListener
    {
        public static readonly int BreachEffect = GemDef.NetIdOf("trait_broadside_breach");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private GhostcaptainRules _rules;

        public GhostcaptainMechanics(GhostcaptainRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(GhostcaptainRules rules) => _rules = rules;

        private GhostcaptainState S => _state.Ghostcaptain;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Ghostcaptain);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Broadside => On && BroadsidePower > 0;
        public bool Crew => On && CrewPower > 0;
        public bool Parrot => On && ParrotPower > 0;
        public bool Traited => Broadside || Crew || Parrot;

        public double BroadsidePower => Positive(GhostcaptainStats.BroadsidePower);
        public double CrewPower => Positive(GhostcaptainStats.CrewPower);
        public double ParrotPower => Positive(GhostcaptainStats.ParrotPower);

        public int Cannons => System.Math.Max(1, System.Math.Min(_rules.CannonsMax, _rules.Cannons + (int)System.Math.Round(_hero.Value(GhostcaptainStats.BroadsideCannons, 0))));

        public int CannonHits => System.Math.Max(_rules.CannonHitsMin, (int)System.Math.Round(_rules.CannonHits + _hero.Value(GhostcaptainStats.BroadsideHits, 0)));

        public double DawnSeconds => System.Math.Max(_rules.DawnSecondsMin, _rules.DawnSeconds + _hero.Value(GhostcaptainStats.CrewDawn, 0));

        public int ParrotEvery => System.Math.Max(_rules.ParrotEveryMin, (int)System.Math.Round(_rules.ParrotEvery + _hero.Value(GhostcaptainStats.ParrotEvery, 0)));

        public double BreachShare => _rules.BreachDamageTaken + Positive(GhostcaptainStats.BroadsideParty);

        public double MatePower => CrewPower * _rules.MateMultiplier * (1 + Positive(GhostcaptainStats.CrewParty));

        public double DamageTaken() => On && !_state.SharedEnemy && S.BreachTicks > 0 ? 1 + BreachShare : 1;

        public void AfterStrike()
        {
            if (!Broadside)
                return;
            if (++S.LoadHits < CannonHits)
                return;
            S.LoadHits = 0;
            Load(1);
        }

        public void OnSkillCast(SkillSlot slot) => OnSkillCast(slot.Skill, slot.CastPower);

        public void OnSkillCast(GemDef skill, double castPower)
        {
            var strike = skill.Skill?.Effect as StrikeEffect;
            var power = castPower * StrikeMultiplier(skill);
            BoonCast(power);
            if (!On)
                return;
            if (strike != null)
                CastEffects(strike);
            if (Parrot && !(strike?.ParrotRepeat ?? false))
            {
                S.LastSkillPower = power;
                OwnCast(skill, power);
            }
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (S.BreachTicks > 0)
                S.BreachTicks--;
            ShareBreach();
            if (Crew)
                TickCrew();
            if (S.MateTicks > 0)
                S.MateTicks--;
            if (Parrot)
                TickParrot();
        }

        public void OnKillRewarded()
        {
            if (!Crew)
                return;
            var count = _state.EnemyType == EnemyType.Boss ? _rules.BossSailors : _state.EnemyType == EnemyType.Guardian ? _rules.GuardianSailors : 1;
            if (Flag(GhostcaptainStats.CrewDouble))
                count *= 2;
            Recruit(count, _state.EnemyType == EnemyType.Boss);
        }

        public void ReceiveParrotCall(int slot)
        {
            if (!Parrot || slot < 0)
                return;
            if (S.PerchSlot == slot)
            {
                S.PerchTicks = Ticks(_rules.PerchSeconds);
                return;
            }
            if (S.PerchSlot >= 0 && !Flag(GhostcaptainStats.ParrotTwo))
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.ParrotPerch, To = (byte)S.PerchSlot, Value = 0 });
            S.PerchSlot = slot;
            S.PerchTicks = Ticks(_rules.PerchSeconds);
            _state.TraitRelays.Add(new TraitRelay
            {
                Kind = TraitRelayKind.ParrotPerch,
                To = (byte)slot,
                Every = (byte)ParrotEvery,
                Value = ParrotPower * (1 + Positive(GhostcaptainStats.ParrotParty)),
                Seconds = _rules.PerchSeconds
            });
        }

        private void Load(int cannons)
        {
            S.Loaded = System.Math.Min(Cannons, S.Loaded + cannons);
            var half = Flag(GhostcaptainStats.BroadsideHalf) ? (Cannons + 1) / 2 : Cannons;
            if (S.Loaded >= half)
                FireVolley(S.Loaded, !Flag(GhostcaptainStats.BroadsideHalf) && !S.Mixed);
        }

        private void FireVolley(int cannons, bool sameEnemy)
        {
            if (cannons <= 0)
                return;
            S.Volleys++;
            var power = cannons * BroadsidePower * (sameEnemy ? _rules.SameEnemy : 1);
            TraitHit(power);
            if (Flag(GhostcaptainStats.BroadsideQueue))
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, power * _rules.VolleyQueueShare * _state.FocusMultiplier, StatTags.Keyboard));
            if (Flag(GhostcaptainStats.BroadsidePowder))
                _state.PendingResource += _rules.VolleyPowder;
            S.Loaded = Flag(GhostcaptainStats.BroadsideLoaded) ? System.Math.Min(_rules.LoadedAfter, Cannons - 1) : 0;
            S.Mixed = false;
            if (Flag(GhostcaptainStats.BroadsideNoBreach))
                return;
            S.BreachTicks = Ticks(_rules.BreachSeconds);
            S.BreachResendTicks = 0;
            if (_state.FightingBoss && !_state.SharedEnemy)
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, S.BreachTicks);
        }

        private void ShareBreach()
        {
            if (!InParty || S.BreachTicks <= 0 || S.BreachResendTicks-- > 0)
                return;
            S.BreachResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(BreachEffect, BreachShare, System.Math.Min(_rules.BreachEffectSeconds, S.BreachTicks * GameLoop.TickSeconds), 1);
        }

        private void Recruit(int count, bool boss)
        {
            if (S.DawnTicks <= 0 && S.Sailors == 0)
                S.DawnTicks = Ticks(DawnSeconds);
            S.Sailors += count;
            if (boss)
                S.BossSailors += count;
        }

        private void TickCrew()
        {
            if (S.Sailors > 0 && _state.EnemyAlive)
            {
                S.CrewCarry += _rules.CrewHitsPerSecond * GameLoop.TickSeconds;
                while (S.CrewCarry >= 1 - 1e-9)
                {
                    S.CrewCarry -= 1;
                    CrewHit(S.Sailors * CrewPower);
                }
            }
            if (S.DawnTicks > 0 && --S.DawnTicks == 0)
                Dawn();
        }

        private void CrewHit(double multiplier)
        {
            TraitHit(multiplier);
            if (Flag(GhostcaptainStats.CrewQueue))
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _rules.CrewQueueShare * _state.FocusMultiplier, StatTags.Keyboard));
        }

        private void Dawn()
        {
            if (S.Sailors <= 0)
                return;
            S.Dawns++;
            if (Flag(GhostcaptainStats.CrewFarewell) && !Flag(GhostcaptainStats.CrewNoFarewell))
                TraitHit(S.Sailors * CrewPower * _rules.Farewell);
            SendMate();
            var keep = Flag(GhostcaptainStats.CrewKeep) ? (int)System.Math.Round(S.Sailors * _rules.CrewKeep) : 0;
            if (Flag(GhostcaptainStats.CrewKeepBoss))
                keep = System.Math.Max(keep, S.BossSailors);
            S.Sailors = keep;
            S.BossSailors = System.Math.Min(S.BossSailors, keep);
            S.DawnTicks = keep > 0 ? Ticks(DawnSeconds) : 0;
        }

        private void SendMate()
        {
            if (!InParty)
            {
                S.MatePower = MatePower;
                S.MateTicks = Ticks(DawnSeconds);
                return;
            }
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Mate, To = LanternbearerMechanics.AnyAlly, Value = MatePower, Seconds = DawnSeconds });
        }

        private void TickParrot()
        {
            if (S.PerchTicks > 0 && --S.PerchTicks == 0)
                S.PerchSlot = -1;
            if (!InParty || S.OfferTicks-- > 0)
                return;
            S.OfferTicks = Ticks(_rules.OfferSeconds) - 1;
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.ParrotOffer, To = TraitRelay.AllSlots, Seconds = _rules.OfferSeconds * 2 });
        }

        private void OwnCast(GemDef skill, double power)
        {
            if (S.PerchSlot >= 0 && !Flag(GhostcaptainStats.ParrotTwo))
                return;
            if (Flag(GhostcaptainStats.ParrotHeavy) && (skill.Skill?.CooldownSeconds ?? 0) < _rules.HeavyCooldownSeconds)
                return;
            if (++S.ParrotCasts < ParrotEvery)
                return;
            S.ParrotCasts = 0;
            Repeat(power);
        }

        private void Repeat(double power)
        {
            S.Repeats++;
            var share = ParrotPower;
            var twice = Flag(GhostcaptainStats.ParrotTwo) && !InParty;
            if (twice)
                share *= _rules.ParrotTwoShare;
            for (var i = 0; i < (twice ? 2 : 1); i++)
                RepeatHit(power * share);
            if (Flag(GhostcaptainStats.ParrotPowder))
                _state.PendingResource += _rules.ParrotPowder;
        }

        private void RepeatHit(double multiplier)
        {
            if (Flag(GhostcaptainStats.ParrotCrit))
                TraitHit(multiplier, 1);
            else
                TraitHit(multiplier);
            if (Flag(GhostcaptainStats.ParrotQueue))
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _rules.ParrotQueueShare * _state.FocusMultiplier, StatTags.Keyboard));
        }

        private void CastEffects(StrikeEffect strike)
        {
            if (strike.Cannons > 0 && Broadside)
                Load(strike.Cannons);
            if (strike.Volley && Broadside)
            {
                S.Loaded = Cannons;
                FireVolley(Cannons, !S.Mixed);
            }
            if (strike.Sailors > 0 && Crew)
                Recruit(strike.Sailors, false);
            if (strike.CrewHit > 0 && S.Sailors > 0)
                CrewHit(S.Sailors * CrewPower * strike.CrewHit);
            if (strike.ParrotRepeat && Parrot && S.LastSkillPower > 0)
                Repeat(S.LastSkillPower);
            if (S.MateTicks > 0)
            {
                S.MateShots++;
                TraitHit(S.MatePower);
            }
        }

        private void BoonCast(double power)
        {
            if (Boons.MateTicks > 0 && Boons.MatePower > 0)
            {
                Boons.MateShots++;
                TraitHit(Boons.MatePower);
            }
            if (Boons.ParrotTicks > 0 && Boons.ParrotPower > 0)
            {
                if (++Boons.ParrotCasts >= System.Math.Max(1, Boons.ParrotEvery))
                {
                    Boons.ParrotCasts = 0;
                    Boons.ParrotRepeats++;
                    TraitHit(power * Boons.ParrotPower);
                }
                Boons.ParrotTicks = System.Math.Max(Boons.ParrotTicks, Ticks(_rules.PerchSeconds) / 2);
                Boons.ParrotCallOwed = true;
            }
            else if (Boons.ParrotOfferTicks > 0 && Boons.ParrotOwner >= 0)
                Boons.ParrotCallOwed = true;
        }

        private void TickBoons()
        {
            if (Boons.MateTicks > 0)
                Boons.MateTicks--;
            if (Boons.ParrotTicks > 0)
                Boons.ParrotTicks--;
            if (Boons.ParrotOfferTicks > 0)
                Boons.ParrotOfferTicks--;
            if (Boons.ParrotCallTicks > 0)
            {
                Boons.ParrotCallTicks--;
                return;
            }
            if (!Boons.ParrotCallOwed || Boons.ParrotOwner < 0)
                return;
            Boons.ParrotCallOwed = false;
            Boons.ParrotCallTicks = Ticks(_rules.CallSeconds);
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.ParrotCall, To = (byte)Boons.ParrotOwner });
        }

        private double StrikeMultiplier(GemDef skill) =>
            skill.Skill?.Effect is StrikeEffect strike && strike.Multiplier > 0 ? strike.Multiplier : _rules.StrikeFallback;

        private void TraitHit(double multiplier, double extraCrit = 0)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard, extraCrit);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
