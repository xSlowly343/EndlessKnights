using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class WindshotMechanics
    {
        private const int StillCap = 1_000_000;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private WindshotRules _rules;

        public WindshotMechanics(WindshotRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(WindshotRules rules) => _rules = rules;

        private WindshotState S => _state.Windshot;
        private bool On => _state.Is(HeroClass.Windshot);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));

        public bool LongShot => On && ShotPower > 0;
        public bool Calm => On && Positive(WindshotStats.CalmPower) > 0;
        public bool Tailwind => On && GustPower > 0;

        public double ShotPower => Positive(WindshotStats.ShotPower);
        public double GustPower => Positive(WindshotStats.GustPower);

        public int ShotEvery => System.Math.Max(_rules.ShotEveryMin, (int)System.Math.Round(_rules.ShotEvery + _hero.Value(WindshotStats.ShotEvery, 0)));

        public double ShotSpend => System.Math.Max(0, _rules.ShotSpend + _hero.Value(WindshotStats.ShotSpend, 0));

        public double ShotMultiplier(double tension) => 1 + ShotPower * tension / _rules.ShotPer;

        public int StillTicksNeeded => Ticks(System.Math.Max(_rules.CalmStillMinSeconds, _rules.CalmStillSeconds + _hero.Value(WindshotStats.CalmStill, 0)));

        public bool InCalm => Calm && (Flag(WindshotStats.CalmEternal) || S.ForcedCalmTicks > 0 || S.StillTicks >= StillTicksNeeded);

        public double CalmPenalty
        {
            get
            {
                var penalty = Flag(WindshotStats.CalmEternal)
                    ? _rules.CalmEternalPenalty
                    : System.Math.Min(1, System.Math.Max(0, _rules.CalmCritPenalty + _hero.Value(WindshotStats.CalmPenalty, 0)));
                return Flag(WindshotStats.CalmDead) ? penalty * _rules.CalmDeadShare : penalty;
            }
        }

        public double GustPxNeeded => System.Math.Max(_rules.GustPxMin, _rules.GustPx + _hero.Value(WindshotStats.GustPx, 0));

        public int GustHitsPerGust => System.Math.Max(_rules.GustHitsMin, (int)System.Math.Round(_rules.GustHits + _hero.Value(WindshotStats.GustHits, 0)));

        public int GustMax => System.Math.Max(1, (int)System.Math.Round(_rules.GustMax + _hero.Value(WindshotStats.GustMax, 0)));

        public int ExtraArrows => System.Math.Max(1, 1 + (int)System.Math.Round(_hero.Value(WindshotStats.GustArrows, 0)));

        public double StrikeMultiplier()
        {
            if (!Calm)
                return 1;
            var multiplier = 1.0;
            if (InCalm)
            {
                if (S.CalmFirstPending && Flag(WindshotStats.CalmFirst))
                    multiplier *= _rules.CalmFirstMultiplier;
            }
            else if (Flag(WindshotStats.CalmDead))
                multiplier *= _rules.CalmDeadOutside;
            if (S.StormHitsLeft > 0)
                multiplier *= _rules.CalmStormMultiplier;
            return multiplier;
        }

        public bool ForcesCrit(StatTags tags) => (tags & StatTags.Keyboard) != 0 && InCalm;

        public double CritScale(StatTags tags) => ForcesCrit(tags) ? 1 - CalmPenalty : 1;

        public void AfterStrike(bool crit)
        {
            if (!On)
                return;
            var paused = S.LastHitTick >= 0 && _state.Tick - S.LastHitTick >= Ticks(_rules.ShotPauseSeconds);
            S.LastHitTick = _state.Tick;
            if (Calm)
                CalmStep(crit);
            if (Tailwind && S.GustActive)
                GustArrows();
            if (LongShot && !Flag(WindshotStats.ShotAuto))
                ShotStep(paused);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.Tension > 0)
                _state.PendingResource += strike.Tension;
            if (Calm && strike.CalmSeconds > 0)
                S.ForcedCalmTicks = System.Math.Max(S.ForcedCalmTicks, Ticks(strike.CalmSeconds));
            if (Tailwind && strike.Gusts > 0)
                AddGusts(strike.Gusts);
            if (LongShot && strike.Shot)
                Shot(1);
        }

        public void Tick(double mousePx)
        {
            if (!On)
            {
                _state.TraitPartyCritMultiplier = 0;
                _state.TraitPartyCooldown = 0;
                _state.StepTensionBlocked = false;
                return;
            }
            TickCalm(mousePx);
            TickGusts(mousePx);
            TickAutoShot();
            _state.StepTensionBlocked = InCalm && Positive(WindshotStats.CalmDrift) > 0;
            _state.TraitPartyCritChance = LongShot && _state.HunterMarkTicks > 0 ? _rules.ShotPartyCrit + Positive(WindshotStats.ShotPartyCrit) : 0;
            _state.TraitPartyCritMultiplier = InCalm ? _rules.CalmPartyCritMultiplier + Positive(WindshotStats.CalmPartyCrit) : 0;
            _state.TraitPartyCooldown = Tailwind && S.GustPartyTicks > 0
                ? (_rules.GustPartyCooldown + Positive(WindshotStats.GustPartyCooldown)) * (Flag(WindshotStats.GustPartyDouble) ? 2 : 1)
                : 0;
        }

        private void ShotStep(bool paused)
        {
            if (paused && Flag(WindshotStats.ShotPause))
            {
                S.ShotProgress = 0;
                Shot(1);
                return;
            }
            if (++S.ShotProgress < ShotEvery)
                return;
            S.ShotProgress = 0;
            Shot(1);
        }

        private void TickAutoShot()
        {
            if (!LongShot || !Flag(WindshotStats.ShotAuto))
            {
                S.AutoShotTicks = 0;
                return;
            }
            if (++S.AutoShotTicks < Ticks(_rules.ShotAutoSeconds))
                return;
            S.AutoShotTicks = 0;
            Shot(_rules.ShotAutoMultiplier);
        }

        private void Shot(double scale)
        {
            if (!_state.EnemyAlive)
                return;
            var tension = System.Math.Max(0, _state.Resource);
            var full = _state.ResourceFill >= 1 - 1e-9;
            var multiplier = ShotMultiplier(tension) * scale;
            var spend = System.Math.Min(tension, ShotSpend);
            if (full && Flag(WindshotStats.ShotStorm))
            {
                multiplier *= _rules.ShotStormMultiplier;
                spend = tension;
            }
            _state.Resource = tension - spend;
            if (full)
                _combat.Class.PlaceHunterMark();
            S.Shots++;
            var spawns = S.Spawns;
            TraitHit(multiplier);
            if (Flag(WindshotStats.ShotPierce))
                Queue(multiplier * _rules.ShotPierceShare);
            if (Flag(WindshotStats.ShotRefund) && S.Spawns != spawns)
                _state.PendingResource += spend;
        }

        private void CalmStep(bool crit)
        {
            if (S.StormHitsLeft > 0)
                S.StormHitsLeft--;
            if (!InCalm)
                return;
            S.CalmFirstPending = false;
            if (crit && Flag(WindshotStats.CalmTension))
                _state.PendingResource += _rules.CalmTensionPerCrit;
        }

        private void TickCalm(double mousePx)
        {
            if (S.ForcedCalmTicks > 0)
                S.ForcedCalmTicks--;
            if (!Calm)
            {
                S.StillTicks = 0;
                S.WasCalm = false;
                S.CalmFirstPending = false;
                S.StormHitsLeft = 0;
                return;
            }
            var drift = Positive(WindshotStats.CalmDrift) * GameLoop.TickSeconds;
            if (mousePx <= drift + 1e-9)
                S.StillTicks = System.Math.Min(StillCap, S.StillTicks + 1);
            else
                S.StillTicks = 0;
            var calm = InCalm;
            if (calm && !S.WasCalm)
                S.CalmFirstPending = true;
            if (!calm && S.WasCalm && Flag(WindshotStats.CalmStorm))
                S.StormHitsLeft = _rules.CalmStormHits;
            S.WasCalm = calm;
        }

        private void TickGusts(double mousePx)
        {
            if (!Tailwind)
            {
                S.Gusts = 0;
                S.GustHitsLeft = 0;
                S.GustStormTicks = 0;
                S.GustPartyTicks = 0;
                S.GustPxCarry = 0;
                return;
            }
            if (S.GustStormTicks > 0)
                S.GustStormTicks--;
            if (S.GustPartyTicks > 0)
                S.GustPartyTicks--;
            S.GustPxCarry += mousePx;
            var need = GustPxNeeded;
            var gusts = 0;
            for (; S.GustPxCarry >= need; S.GustPxCarry -= need)
                gusts++;
            if (Flag(WindshotStats.GustTimer) && ++S.GustTimerTicks >= Ticks(_rules.GustTimerSeconds))
            {
                S.GustTimerTicks = 0;
                gusts++;
            }
            if (gusts > 0)
                AddGusts(gusts);
            StartGust();
        }

        private void AddGusts(int count)
        {
            if (Flag(WindshotStats.GustStorm))
            {
                S.Gusts = System.Math.Min(_rules.GustStormGusts, S.Gusts + count);
                if (S.Gusts < _rules.GustStormGusts)
                    return;
                S.Gusts = 0;
                S.GustStormTicks = Ticks(_rules.GustStormSeconds);
                OnGustStart();
                return;
            }
            S.Gusts = System.Math.Min(GustMax, S.Gusts + count);
            StartGust();
        }

        private void StartGust()
        {
            if (Flag(WindshotStats.GustStorm) || S.GustHitsLeft > 0 || S.Gusts <= 0)
                return;
            S.Gusts--;
            S.GustHitsLeft = GustHitsPerGust;
            OnGustStart();
        }

        private void OnGustStart()
        {
            S.GustsStarted++;
            S.GustPartyTicks = Ticks(_rules.GustPartySeconds);
            _state.PendingResource += _rules.GustTension + Positive(WindshotStats.GustTension);
        }

        private void GustArrows()
        {
            var multiplier = GustPower * ExtraArrows;
            TraitHit(multiplier);
            if (Flag(WindshotStats.GustQueue))
                Queue(multiplier * _rules.GustQueueShare);
            if (S.GustStormTicks > 0 || S.GustHitsLeft <= 0)
                return;
            S.GustHitsLeft--;
            StartGust();
        }

        private void Queue(double multiplier)
        {
            if (multiplier <= 0)
                return;
            _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard));
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
