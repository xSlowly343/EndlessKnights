using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class RunedancerMechanics
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private RunedancerRules _rules;

        public RunedancerMechanics(RunedancerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(RunedancerRules rules) => _rules = rules;

        private RunedancerState S => _state.Runedancer;
        private bool On => _state.Is(HeroClass.Runedancer);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;

        public bool Dance => On && WordPower > 0;
        public bool Blades => On && BladePower > 0;
        public bool Whirl => On && WhirlPower > 0;

        public double WordPower => System.Math.Max(0, _hero.Value(RunedancerStats.RuneWord, 0));
        public double BladePower => System.Math.Max(0, _hero.Value(RunedancerStats.BladeDamage, 0));
        public double WhirlPower => System.Math.Max(0, _hero.Value(RunedancerStats.WhirlDamage, 0));

        public int WordLength =>
            System.Math.Max(_rules.WordLengthMin, (int)System.Math.Round(_rules.WordLength + _hero.Value(RunedancerStats.RuneWordLength, 0) - (Flag(RunedancerStats.RuneCalligrapher) ? 1 : 0)));

        public int BladeMax => System.Math.Max(1, (int)System.Math.Round(_rules.BladeMax + _hero.Value(RunedancerStats.BladeMax, 0)));

        public int WhirlMax => Flag(RunedancerStats.WhirlPalm)
            ? _rules.WhirlPalmMax
            : System.Math.Max(1, (int)System.Math.Round(_rules.WhirlMax + _hero.Value(RunedancerStats.WhirlMax, 0)));

        private double HitWeight => 1 + System.Math.Max(0, _hero.Value(RunedancerStats.TraitHitWeight, 0));

        public double StrikeMultiplier()
        {
            if (!Dance)
                return 1;
            if (Flag(RunedancerStats.RuneSilent))
                return 0;
            return S.Runes < 1e-9 ? 1 + System.Math.Max(0, _hero.Value(RunedancerStats.RuneFirstRune, 0)) : 1;
        }

        public double SkillMultiplier(StatTags tags)
        {
            if ((tags & StatTags.Skill) == 0 || !Whirl)
                return 1;
            var perFive = System.Math.Max(0, _hero.Value(RunedancerStats.WhirlSkillPerFive, 0));
            return 1 + perFive * (S.WhirlBlades / 5);
        }

        public void AfterStrike(bool crit)
        {
            if (!On)
                return;
            var weight = HitWeight;
            S.HitsThisTick += weight;
            if (crit)
                S.CritsThisTick++;
            if (Dance)
            {
                var runes = weight;
                if (S.DoubleRunesLeft > 0)
                {
                    S.DoubleRunesLeft--;
                    runes *= 2;
                }
                LayRunes(runes);
            }
            if (Blades)
                BladeStrike(weight, crit);
            if (Whirl && Flag(RunedancerStats.WhirlPalm) && S.WhirlBlades > 0)
                TraitHit(WhirlPower * S.WhirlBlades, StatTags.Keyboard);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On)
                return;
            var strike = skill.Skill?.Effect as StrikeEffect;
            var writes = !Flag(RunedancerStats.RuneCalligrapher);
            if (Dance && writes && Flag(RunedancerStats.RuneOnCast))
                LayRunes(1);
            if (strike == null)
                return;
            if (Dance && writes && strike.Runes > 0)
                LayRunes(strike.Runes);
            if (Dance && strike.Word)
                LayRunes(System.Math.Max(0, WordLength - S.Runes));
            if (Blades && strike.Blades > 0)
                AddBlades(strike.Blades);
            if (Blades && strike.BladeRain)
                Rain();
            if (Whirl && strike.WhirlSeconds > 0)
                S.WhirlSpinTicks = System.Math.Max(S.WhirlSpinTicks, Ticks(strike.WhirlSeconds));
        }

        public bool Forbids(GemDef skill) =>
            On && skill.Skill?.Effect is StrikeEffect { BladeRain: true } && Flag(RunedancerStats.BladeAutoRain);

        public void Tick()
        {
            if (!On)
            {
                S.WhirlFull = false;
                return;
            }
            if (S.NewEnemy)
                CarryToNewEnemy();
            Shadow();
            FadeRunes();
            FallBlades();
            SpinWhirl();
            S.WhirlFull = Whirl && S.WhirlBlades >= WhirlMax;
            if (S.SealTicks > 0)
                S.SealTicks--;
        }

        public void EndTick() => S.PushHits();

        private void CarryToNewEnemy()
        {
            S.NewEnemy = false;
            if (Flag(RunedancerStats.RuneCarry))
                S.Runes = S.RunesBefore;
            var keep = System.Math.Min(1, System.Math.Max(0, _hero.Value(RunedancerStats.BladeKeep, 0)));
            S.Blades = System.Math.Min(BladeMax, (int)System.Math.Floor(S.BladesBefore * keep + 1e-9));
        }

        private void LayRunes(double runes)
        {
            S.Runes += runes;
            S.RuneIdleTicks = 0;
            var length = WordLength;
            while (S.Runes >= length - 1e-9)
            {
                S.Runes = System.Math.Max(0, S.Runes - length);
                Word();
            }
        }

        private void Word()
        {
            S.Words++;
            var power = WordPower;
            if (!S.WordOnEnemy)
                power *= 1 + System.Math.Max(0, _hero.Value(RunedancerStats.RuneCapital, 0));
            S.WordOnEnemy = true;
            var weave = System.Math.Max(0, _hero.Value(RunedancerStats.RuneWeave, 0));
            if (weave <= 0)
            {
                TraitHit(power, StatTags.Keyboard);
                var rhyme = System.Math.Min(1, System.Math.Max(0, _hero.Value(RunedancerStats.RuneRhyme, 0)));
                if (rhyme > 0)
                    _state.QueueDamage[0] += _combat.SkillDamage(_combat.BaseKeyDamage, power * rhyme * _state.FocusMultiplier, StatTags.Keyboard);
            }
            var seconds = weave > 0 ? _rules.WeaveSeconds : _rules.SealSeconds + _hero.Value(RunedancerStats.RuneSealSeconds, 0);
            if (seconds > 0)
                S.SealTicks = System.Math.Max(S.SealTicks, Ticks(seconds));
            S.DoubleRunesLeft = (int)System.Math.Round(System.Math.Max(0, _hero.Value(RunedancerStats.RuneDouble, 0)));
            if (Flag(RunedancerStats.RuneSilent))
                RefreshRightSkill();
        }

        private void RefreshRightSkill()
        {
            var index = SkillLoadout.IndexOf(_state.Skills, SkillSlotRole.Right);
            if (index >= 0)
                _state.Skills[index].CooldownTicks = 0;
        }

        private void FadeRunes()
        {
            if (S.Runes <= 0)
                return;
            var fade = Ticks(System.Math.Max(0.1, _rules.FadeSeconds + _hero.Value(RunedancerStats.RuneFadeSeconds, 0)));
            if (++S.RuneIdleTicks >= fade)
                S.Runes = 0;
        }

        private void BladeStrike(double weight, bool crit)
        {
            var fast = Flag(RunedancerStats.BladeBossFast) && _state.FightingBoss ? 2 : 1;
            if (Flag(RunedancerStats.BladeOnCrit))
            {
                if (crit)
                    AddBlades(fast);
            }
            else
            {
                S.BladeProgress += weight * fast;
                var every = System.Math.Max(1, (int)System.Math.Round(_rules.BladeEvery + _hero.Value(RunedancerStats.BladeEvery, 0)));
                var whole = (int)System.Math.Floor(S.BladeProgress / every + 1e-9);
                if (whole > 0)
                {
                    S.BladeProgress -= whole * every;
                    AddBlades(whole);
                }
            }
            if (crit && S.Blades > 0 && Flag(RunedancerStats.BladeShard))
            {
                S.Blades--;
                TraitHit(BladePower * _rules.BladeShardMultiplier, StatTags.Keyboard);
            }
        }

        private void AddBlades(int count)
        {
            S.Blades = System.Math.Min(BladeMax, S.Blades + count);
            if (S.Blades >= BladeMax && Flag(RunedancerStats.BladeAutoRain))
                Rain();
        }

        private void Rain()
        {
            S.Rains++;
            var blades = S.Blades;
            S.Blades = 0;
            if (blades > 0)
                TraitHit(BladePower * blades, StatTags.Keyboard);
        }

        private void FallBlades()
        {
            if (!Blades || !Flag(RunedancerStats.BladeFall) || S.Blades <= 0)
            {
                S.BladeFallTicks = 0;
                return;
            }
            if (++S.BladeFallTicks < Ticks(_rules.BladeFallSeconds))
                return;
            S.BladeFallTicks = 0;
            S.Blades--;
            TraitHit(BladePower, StatTags.Keyboard);
        }

        private void SpinWhirl()
        {
            if (!Whirl)
            {
                S.WhirlBlades = 0;
                S.WhirlCarry = 0;
                return;
            }
            var cap = WhirlMax;
            var target = System.Math.Min(cap, WhirlTarget());
            if (S.WhirlSpinTicks > 0)
            {
                S.WhirlSpinTicks--;
                target = cap;
            }
            var gale = Flag(RunedancerStats.WhirlGale);
            if (target >= S.WhirlBlades)
            {
                S.WhirlBlades = target;
                S.WhirlLingerTicks = gale ? 0 : Ticks(System.Math.Max(0, _hero.Value(RunedancerStats.WhirlLinger, 0)));
            }
            else if (gale)
                S.WhirlBlades = target;
            else if (Flag(RunedancerStats.WhirlBossHold) && _state.FightingBoss)
                S.WhirlLingerTicks = System.Math.Max(S.WhirlLingerTicks, 1);
            else if (S.WhirlLingerTicks > 0)
                S.WhirlLingerTicks--;
            else
                S.WhirlBlades = target;
            if (Flag(RunedancerStats.WhirlPalm) || S.WhirlBlades <= 0)
            {
                S.WhirlCarry = 0;
                return;
            }
            S.WhirlCarry += _rules.WhirlHitsPerSecond * (gale ? 2 : 1) * GameLoop.TickSeconds;
            var strikes = System.Math.Floor(S.WhirlCarry + 1e-9);
            if (strikes < 1)
                return;
            S.WhirlCarry -= strikes;
            var eye = Flag(RunedancerStats.WhirlEye) && S.WhirlBlades >= cap ? _rules.WhirlEyeMultiplier : 1;
            TraitHit(WhirlPower * S.WhirlBlades * strikes * eye, StatTags.Keyboard);
        }

        private int WhirlTarget()
        {
            var steady = (int)System.Math.Round(System.Math.Max(0, _hero.Value(RunedancerStats.WhirlSteady, 0)));
            var target = steady > 0 ? steady : (int)System.Math.Floor(S.HitsLastSecond + 1e-9);
            target += (int)System.Math.Round(System.Math.Max(0, _hero.Value(RunedancerStats.WhirlExtra, 0)));
            if (Flag(RunedancerStats.WhirlGust))
                target += S.Crits(Ticks(_rules.WhirlGustSeconds));
            return target;
        }

        private void Shadow()
        {
            if (!Flag(RunedancerStats.ShadowDancer) || _rules.ShadowHitsPerSecond <= 0
                || _state.KeylessTicks * GameLoop.TickSeconds < _rules.ShadowAfterSeconds)
            {
                S.ShadowCarry = 0;
                return;
            }
            S.ShadowCarry += _rules.ShadowHitsPerSecond * GameLoop.TickSeconds;
            for (; S.ShadowCarry >= 1 - 1e-9; S.ShadowCarry -= 1)
            {
                TraitHit(1, StatTags.Keyboard);
                AfterStrike(_combat.LastHitCrit);
            }
        }

        private void TraitHit(double multiplier, StatTags tags)
        {
            if (multiplier <= 0)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, tags);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
