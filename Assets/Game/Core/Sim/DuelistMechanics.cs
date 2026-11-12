using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class DuelistMechanics
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private DuelistRules _rules;

        public DuelistMechanics(DuelistRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(DuelistRules rules) => _rules = rules;

        private DuelistState S => _state.Duelist;
        private bool On => _state.Is(HeroClass.Duelist);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));

        public bool Breach => On && LungePower > 0;
        public bool Touche => On && TouchePower > 0;
        public bool Phantom => On && PhantomPower > 0;

        public double LungePower => Positive(DuelistStats.BreachLunge);
        public double TouchePower => Positive(DuelistStats.TouchePower);
        public double PhantomPower => Positive(DuelistStats.PhantomPower);

        public double LungeMultiplier => (1 + LungePower) * (Flag(DuelistStats.BreachDouble) ? 2 * _rules.BreachDoubleShare : 1);

        public int BreachTicks => Ticks(System.Math.Max(0.1, _rules.BreachSeconds + _hero.Value(DuelistStats.BreachSeconds, 0)));

        public int BreachEveryTicks
        {
            get
            {
                var seconds = _rules.BreachEverySeconds + _hero.Value(DuelistStats.BreachEvery, 0);
                if (Flag(DuelistStats.BreachBossFast) && _state.FightingBoss)
                    seconds -= _rules.BreachBossSooner;
                return Ticks(System.Math.Max(_rules.BreachEveryMinSeconds, seconds));
            }
        }

        public int MarksNeeded =>
            System.Math.Max(_rules.ToucheMarksMin, (int)System.Math.Round(_rules.ToucheMarks + _hero.Value(DuelistStats.ToucheMarks, 0) - (Flag(DuelistStats.ToucheFlawless) ? 1 : 0)));

        public int TouchEvery => System.Math.Max(_rules.ToucheEveryMin, (int)System.Math.Round(_rules.ToucheEvery + _hero.Value(DuelistStats.ToucheEvery, 0)));

        public double ExecuteShare
        {
            get
            {
                var share = System.Math.Min(_rules.ToucheExecuteMax, Positive(DuelistStats.ToucheExecute));
                return _state.FightingBoss ? share * _rules.ToucheBossShare : share;
            }
        }

        public double PhantomThreshold => _rules.PhantomHitsPerSecond + _hero.Value(DuelistStats.PhantomThreshold, 0);

        private bool Exposing => Positive(DuelistStats.BreachExpose) > 0;

        public double StrikeMultiplier()
        {
            if (!On)
                return 1;
            if (Phantom && Flag(DuelistStats.PhantomSwap))
                return 0;
            var multiplier = 1.0;
            if (Phantom)
                multiplier *= System.Math.Max(0, 1 - _rules.PhantomSelfPenalty - _hero.Value(DuelistStats.PhantomSelfPenalty, 0));
            if (Breach)
            {
                if (S.BreachOpen && !Exposing)
                    multiplier *= LungeMultiplier;
                else if (!S.BreachOpen && Flag(DuelistStats.BreachRead))
                    multiplier *= 1 - _rules.BreachOutsidePenalty;
            }
            if (Touche)
            {
                multiplier *= 1 + System.Math.Min(_rules.ToucheMarkDamageMax, Positive(DuelistStats.ToucheMarkDamage) * S.Marks);
                multiplier *= System.Math.Max(0, 1 - Positive(DuelistStats.ToucheOffPenalty));
            }
            return multiplier;
        }

        public void AfterStrike(bool crit)
        {
            if (!On)
                return;
            S.HitsThisTick++;
            if (Breach && S.BreachOpen)
                Lunge();
            if (Touche)
                TouchStep(crit);
            if (Phantom && S.PhantomFighting && !Flag(DuelistStats.PhantomPause))
                PhantomStrike();
        }

        public void OnSkillCast(GemDef skill, SkillDef? variant = null)
        {
            if (!On)
                return;
            var strike = (variant ?? skill.Skill)?.CastStrike;
            if (Breach && (Flag(DuelistStats.BreachOnCast) || strike is { Breach: true }))
                OpenBreach();
            if (strike == null)
                return;
            if (Touche && strike.Touches > 0 && !Flag(DuelistStats.ToucheFlawless))
                AddMarks(strike.Touches);
            if (Phantom && strike.PhantomSeconds > 0)
                S.PhantomSwitchTicks = System.Math.Max(S.PhantomSwitchTicks, Ticks(strike.PhantomSeconds));
            var echo = Positive(DuelistStats.PhantomEcho);
            if (Phantom && S.PhantomFighting && echo > 0 && !Flag(DuelistStats.PhantomFeint))
                TraitHit(strike.Multiplier * echo);
        }

        public void Tick()
        {
            if (!On)
                return;
            if (S.NewEnemy)
                CarryToNewEnemy();
            TickBreach();
            TickPhantom();
        }

        public void EndTick() => S.PushHits();

        private void CarryToNewEnemy()
        {
            S.NewEnemy = false;
            var marks = Flag(DuelistStats.ToucheKeep) ? S.MarksBefore : S.CarryMarks;
            S.CarryMarks = 0;
            S.Marks = System.Math.Max(0, System.Math.Min(MarksNeeded - 1, marks));
        }

        private void TickBreach()
        {
            if (!Breach)
            {
                S.BreachOpenTicks = 0;
                S.BreachWaitTicks = 0;
                return;
            }
            if (Flag(DuelistStats.BreachAlways))
            {
                S.BreachOpenTicks = 1;
                return;
            }
            if (S.BreachOpenTicks > 0)
            {
                if (--S.BreachOpenTicks == 0)
                    CloseBreach();
                return;
            }
            if (!Flag(DuelistStats.BreachOnCast) && ++S.BreachWaitTicks >= BreachEveryTicks)
                OpenBreach();
        }

        private void OpenBreach()
        {
            if (Flag(DuelistStats.BreachAlways))
                return;
            if (!S.BreachOpen)
            {
                S.LungesInBreach = 0;
                S.BreachPressTicks = 0;
            }
            S.BreachWaitTicks = 0;
            S.BreachOpenTicks = System.Math.Max(S.BreachOpenTicks, BreachTicks);
        }

        private void CloseBreach()
        {
            var riposte = Positive(DuelistStats.BreachRiposte);
            if (riposte > 0 && S.LungesInBreach > 0)
                TraitHit(riposte * S.LungesInBreach);
            S.LungesInBreach = 0;
        }

        private void Lunge()
        {
            S.Lunges++;
            S.LungesInBreach++;
            if (!Flag(DuelistStats.BreachDouble) && !Flag(DuelistStats.BreachAlways))
                _state.PendingResource += (_rules.BreachHonor + _hero.Value(DuelistStats.BreachHonor, 0)) * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Strike);
            Press();
        }

        private void Press()
        {
            var press = Ticks(Positive(DuelistStats.BreachPress));
            var room = Ticks(BreachTicks * GameLoop.TickSeconds * (_rules.BreachPressMax - 1)) - S.BreachPressTicks;
            var add = System.Math.Min(press, room);
            if (add <= 0)
                return;
            S.BreachOpenTicks += add;
            S.BreachPressTicks += add;
        }

        private void TouchStep(bool crit)
        {
            if (Flag(DuelistStats.ToucheOnCrit))
            {
                if (crit)
                    AddMarks(1);
                return;
            }
            if (++S.TouchProgress < TouchEvery)
                return;
            S.TouchProgress = 0;
            AddMarks(1);
        }

        private void AddMarks(int count)
        {
            S.Marks += count;
            var needed = MarksNeeded;
            while (S.Marks >= needed && _state.EnemyAlive)
            {
                S.Marks -= needed;
                Strike();
            }
        }

        private void Strike()
        {
            S.Touches++;
            if (Flag(DuelistStats.TouchePublic))
                return;
            var spawns = S.Spawns;
            TraitHit(TouchePower * BossHalfMultiplier());
            Execute();
            if (!_state.EnemyAlive || S.Spawns != spawns)
                S.CarryMarks = (int)System.Math.Round(Positive(DuelistStats.ToucheCarry));
        }

        private double BossHalfMultiplier()
        {
            var bonus = Positive(DuelistStats.ToucheBossHalf);
            return bonus > 0 && _state.FightingBoss && _state.EnemyHp < _state.EnemyMaxHp * 0.5 ? 1 + bonus : 1;
        }

        private void Execute()
        {
            var share = ExecuteShare;
            if (share > 0 && _state.EnemyAlive && _state.EnemyHp < _state.EnemyMaxHp * share)
                _combat.DirectHit(_state.EnemyHp);
        }

        private void TickPhantom()
        {
            if (!Phantom)
            {
                S.PhantomFighting = false;
                S.PhantomPauseCarry = 0;
                return;
            }
            if (S.PhantomSwitchTicks > 0)
                S.PhantomSwitchTicks--;
            if (Flag(DuelistStats.PhantomSwap))
                S.PhantomFighting = true;
            else if (Flag(DuelistStats.PhantomPause))
                S.PhantomFighting = S.PhantomSwitchTicks > 0 || _state.KeylessTicks * GameLoop.TickSeconds >= _rules.PhantomPauseAfterSeconds;
            else
                S.PhantomFighting = S.PhantomSwitchTicks > 0 || Fast();
            PauseStrikes();
        }

        private bool Fast()
        {
            if (S.HitsLastSecond > PhantomThreshold + 1e-9)
            {
                S.PhantomLingerTicks = Ticks(Positive(DuelistStats.PhantomLinger));
                return true;
            }
            if (S.PhantomFighting && Flag(DuelistStats.PhantomBossHold) && _state.FightingBoss)
                return true;
            if (S.PhantomLingerTicks <= 0)
                return false;
            S.PhantomLingerTicks--;
            return true;
        }

        private void PauseStrikes()
        {
            if (!S.PhantomFighting || !Flag(DuelistStats.PhantomPause) || _rules.PhantomPauseHitsPerSecond <= 0)
            {
                S.PhantomPauseCarry = 0;
                return;
            }
            S.PhantomPauseCarry += _rules.PhantomPauseHitsPerSecond * GameLoop.TickSeconds;
            for (; S.PhantomPauseCarry >= 1 - 1e-9; S.PhantomPauseCarry -= 1)
                PhantomStrike();
        }

        private void PhantomStrike()
        {
            if (Flag(DuelistStats.PhantomFeint))
                return;
            var multiplier = PhantomPower * (Flag(DuelistStats.PhantomTwin) ? 2 : 1);
            if (Flag(DuelistStats.PhantomLunge))
            {
                if (Breach && S.BreachOpen && !Exposing)
                    multiplier *= LungeMultiplier;
                if (Touche)
                    TouchStep(false);
            }
            TraitHit(multiplier);
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Strike);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
