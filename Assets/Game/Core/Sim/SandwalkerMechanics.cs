using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SandwalkerMechanics
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private SandwalkerRules _rules;
        private bool _mirageStrike;

        public SandwalkerMechanics(SandwalkerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(SandwalkerRules rules) => _rules = rules;

        private SandwalkerState S => _state.Sandwalker;
        private bool On => _state.Is(HeroClass.Sandwalker);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));

        public bool Flip => On && FlipPower > 0;
        public bool Caravan => On && CaravanPower > 0;
        public bool Mirage => On && MiragePower > 0;

        public double FlipPower => Positive(SandwalkerStats.FlipPower);
        public double CaravanPower => Positive(SandwalkerStats.CaravanPower);
        public double MiragePower => Positive(SandwalkerStats.MiragePower);

        public int FlipEvery => System.Math.Max(_rules.FlipEveryMin, (int)System.Math.Round(_rules.FlipEvery + _hero.Value(SandwalkerStats.FlipEvery, 0)));

        public double FlipPenalty => System.Math.Min(0.95, System.Math.Max(0, _rules.FlipPenalty + _hero.Value(SandwalkerStats.FlipPenalty, 0)));

        public bool SandFlows => Flip && (!Flag(SandwalkerStats.FlipBossOnly) || _state.FightingBoss);

        public bool OnBottom => Flip && S.Bottom && SandFlows;

        public bool OnTop => !Flip || !OnBottom;

        public double CampPx => System.Math.Max(_rules.CampPxMin, _rules.CampPx + _hero.Value(SandwalkerStats.CampPx, 0));

        public double CampSand => System.Math.Max(0, _rules.CampSand + _hero.Value(SandwalkerStats.CampSand, 0));

        public double CampCutSeconds => System.Math.Max(0, _rules.CampCutSeconds + _hero.Value(SandwalkerStats.CampCut, 0));

        public double MiragePx => System.Math.Max(_rules.MiragePxMin, _rules.MiragePx + _hero.Value(SandwalkerStats.MiragePx, 0));

        public int MirageMax => System.Math.Min(SandwalkerState.MirageCapacity, System.Math.Max(1, (int)System.Math.Round(_rules.MirageMax + _hero.Value(SandwalkerStats.MirageMax, 0))));

        public int MirageTicks => Ticks(System.Math.Max(_rules.MirageSecondsMin, _rules.MirageSeconds + _hero.Value(SandwalkerStats.MirageSeconds, 0)));

        public double MirageTime => System.Math.Max(0, _rules.MirageTime + _hero.Value(SandwalkerStats.MirageTime, 0));

        public double MirageSelfPenalty => System.Math.Min(0.95, System.Math.Max(0, _rules.MirageSelfPenalty + _hero.Value(SandwalkerStats.MirageSelfPenalty, 0)));

        public bool Stepped => S.TicksSinceStep <= Ticks(_rules.StepRecentSeconds);

        public double DamageTaken()
        {
            if (!On)
                return 1;
            var multiplier = _state.BlindTicks > 0 && _state.EnemyAlive ? 1 + _rules.Blind.DamageTaken : 1;
            if (Flip && SandFlows)
                multiplier *= S.Bottom ? 1 - FlipPenalty : 1 + FlipPower;
            else if (Flip)
                multiplier *= 1 + FlipPower;
            if (Mirage && !_mirageStrike)
                multiplier *= 1 - MirageSelfPenalty;
            return multiplier;
        }

        public void Blind(double seconds)
        {
            if (!On || !_state.EnemyAlive || seconds <= 0)
                return;
            _state.BlindTicks = System.Math.Max(_state.BlindTicks, Ticks(seconds + _hero.Value(CombatStats.BlindSeconds, 0)));
        }

        public void AfterStrike()
        {
            if (!On)
                return;
            if (SandFlows)
                Pour(1);
            if (Caravan)
                Walk(Positive(SandwalkerStats.CaravanHitPx));
            if (Mirage)
                MirageStrikeEcho();
            var perMirage = Positive(SandwalkerStats.MirageHits);
            if (!Mirage || perMirage < 1 || ++S.MirageHits < (int)System.Math.Round(perMirage))
                return;
            S.MirageHits = 0;
            RaiseMirages(1);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.Sand > 0)
                _state.PendingResource += strike.Sand;
            if (strike.TopHit > 0 && OnTop)
                TraitHit(strike.TopHit);
            if (strike.AfterStep > 0 && Stepped)
                TraitHit(strike.AfterStep);
            if (strike.StepHit > 0)
                TraitHit(strike.StepHit * System.Math.Min(_rules.StepHitMax, S.StepSinceCastPx / 1000));
            if (strike.StepHit > 0)
                S.StepSinceCastPx = 0;
            if (strike.Pour > 0)
                PourBySkill(strike.Pour);
            if (strike.Camp && Caravan)
                Camp();
            if (strike.Mirages > 0 && Mirage)
                RaiseMirages(strike.Mirages);
            if (strike.BlindSeconds > 0)
                Blind(strike.BlindSeconds);
            if (strike.TimerSeconds > 0)
                AddBossTime(strike.TimerSeconds);
            if (strike.BossTimerStopSeconds > 0 && _state.FightingBoss)
                S.PartyStopTicks = System.Math.Max(S.PartyStopTicks, Ticks(strike.BossTimerStopSeconds));
        }

        public void Tick(double mousePx)
        {
            if (!On)
            {
                _state.TraitPartyTimerSlow = 0;
                _state.TraitPartyResourceGift = 0;
                return;
            }
            if (S.SpawnPending)
                OnNewEnemy();
            TrackStep(mousePx);
            if (SandFlows && Flag(SandwalkerStats.FlipStep))
                StepGrains(mousePx);
            if (Caravan)
                Walk(mousePx > 0 ? mousePx : Flag(SandwalkerStats.CaravanIdle) ? _rules.CaravanIdlePx * GameLoop.TickSeconds : 0);
            TickMirages(mousePx);
            if (OnBottom && _state.FightingBoss && _state.EnemyAlive && !_state.SharedEnemy)
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, 2);
            if (S.CaravanPartyTicks > 0)
                S.CaravanPartyTicks--;
            if (S.CaravanGoldTicks > 0)
                S.CaravanGoldTicks--;
            if (S.PartyTimeTicks > 0)
                S.PartyTimeTicks--;
            if (S.PartyStopTicks > 0)
                S.PartyStopTicks--;
            if (!Flip)
                S.Grains = 0;
            if (!SandFlows)
                S.Bottom = false;
            if (!Mirage)
                S.ClearMirages();
            _state.TraitPartyTimerSlow = PartyTimerSlow();
            _state.TraitPartyCooldown = S.CaravanPartyTicks > 0 ? _rules.CaravanPartyCooldown + Positive(SandwalkerStats.CaravanPartyCooldown) : 0;
            _state.TraitPartyGold = S.CaravanGoldTicks > 0 ? _rules.CaravanPartyGold : 0;
        }

        public double PartyTimerSlow()
        {
            var slow = S.PartyStopTicks > 0 ? 1 : S.PartyTimeTicks > 0 ? _rules.PartyTimeSlow : 0;
            if (!_state.FightingBoss)
                return slow;
            if (OnBottom)
                slow = System.Math.Max(slow, _rules.FlipPartySlow);
            if (Mirage && S.Mirages > 0 && _rules.MirageTime > 0)
                slow = System.Math.Max(slow, System.Math.Min(1, S.Mirages * _rules.MiragePartySlow * MirageTime / _rules.MirageTime));
            return slow;
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            if (!Mirage || !_state.FightingBoss)
                return;
            AddBossTime(MirageTime * S.Mirages, false);
            if (Flag(SandwalkerStats.MirageOnBoss))
                RaiseMirages(1);
        }

        private void TrackStep(double mousePx)
        {
            if (mousePx > 0)
            {
                S.TicksSinceStep = 0;
                S.StepSinceCastPx = System.Math.Min(_rules.StepHitMax * 1000, S.StepSinceCastPx + mousePx);
            }
            else if (S.TicksSinceStep < int.MaxValue / 2)
                S.TicksSinceStep++;
        }

        private void StepGrains(double mousePx)
        {
            S.StepGrainPx += mousePx;
            if (S.StepGrainPx < _rules.FlipStepPx)
                return;
            var grains = (int)(S.StepGrainPx / _rules.FlipStepPx);
            S.StepGrainPx -= grains * _rules.FlipStepPx;
            Pour(grains);
        }

        private void PourBySkill(int grains)
        {
            if (Flip && Flag(SandwalkerStats.FlipBySkill))
            {
                if (SandFlows)
                    Turn();
                return;
            }
            if (SandFlows)
                Pour(grains);
            if (Caravan)
                Walk(grains * _rules.PourPx);
            if (Mirage)
                Haze(grains * _rules.PourPx);
        }

        private void Pour(int grains)
        {
            S.Grains += grains;
            if (S.Grains < FlipEvery)
                return;
            Turn();
        }

        private void Turn()
        {
            S.Grains = 0;
            S.Bottom = !S.Bottom;
            S.Flips++;
            var sand = Positive(SandwalkerStats.FlipSand);
            if (sand > 0)
                _state.PendingResource += sand;
            if (Flag(SandwalkerStats.FlipBlind))
                Blind(_rules.Blind.Seconds);
        }

        private void Walk(double px)
        {
            if (px <= 0)
                return;
            S.RoadPx += px;
            var campPx = CampPx;
            while (S.RoadPx >= campPx)
            {
                S.RoadPx -= campPx;
                Camp();
            }
        }

        private void Camp()
        {
            S.Camps++;
            var oasis = Flag(SandwalkerStats.CaravanOasis) && S.Camps % _rules.OasisEvery == 0;
            TraitHit(CaravanPower * (oasis ? _rules.OasisMultiplier : 1));
            var sand = CampSand * (oasis ? _rules.OasisSandMultiplier : 1);
            _state.PendingResource += sand;
            _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, sand);
            CutCooldowns(CampCutSeconds, Flag(SandwalkerStats.CaravanAll));
            S.CaravanPartyTicks = Ticks(_rules.CaravanPartySeconds);
            if (Flag(SandwalkerStats.CaravanGold))
                S.CaravanGoldTicks = Ticks(_rules.CaravanGoldSeconds);
        }

        private void CutCooldowns(double seconds, bool all)
        {
            var cut = Ticks(seconds);
            if (cut <= 0)
                return;
            SkillSlot? longest = null;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                var slot = _state.Skills[i];
                if (slot.CooldownTicks <= 1)
                    continue;
                if (all)
                    slot.CooldownTicks = System.Math.Max(1, slot.CooldownTicks - cut);
                else if (longest == null || slot.CooldownTicks > longest.CooldownTicks)
                    longest = slot;
            }
            if (longest != null)
                longest.CooldownTicks = System.Math.Max(1, longest.CooldownTicks - cut);
        }

        private void TickMirages(double mousePx)
        {
            if (!Mirage)
                return;
            Haze(mousePx);
            var faded = S.FadeMirages();
            if (faded > 0)
            {
                S.MiragesFaded += faded;
                if (Flag(SandwalkerStats.MirageBlind))
                    Blind(_rules.MirageBlindSeconds);
            }
            S.TrimMirages(MirageMax);
            if (S.Mirages <= 0)
            {
                S.MirageStrikeTicks = 0;
                return;
            }
            if (++S.MirageStrikeTicks < Ticks(_rules.MirageStrikeSeconds))
                return;
            S.MirageStrikeTicks = 0;
            MirageHit(MiragePower * S.Mirages);
        }

        private void Haze(double px)
        {
            if (px <= 0 || !Mirage)
                return;
            S.HazePx += px;
            var perMirage = MiragePx;
            if (S.HazePx < perMirage)
                return;
            var count = (int)(S.HazePx / perMirage);
            S.HazePx -= count * perMirage;
            RaiseMirages(count);
        }

        private void RaiseMirages(int count)
        {
            var max = MirageMax;
            var ticks = MirageTicks;
            for (var i = 0; i < count; i++)
            {
                if (!S.AddMirage(ticks, max))
                    return;
                S.MiragesRaised++;
                AddBossTime(MirageTime, false);
            }
        }

        private void MirageStrikeEcho()
        {
            var echo = Positive(SandwalkerStats.MirageEcho);
            if (echo > 0 && S.Mirages > 0)
                MirageHit(echo * S.Mirages);
        }

        private void MirageHit(double multiplier)
        {
            _mirageStrike = true;
            TraitHit(multiplier);
            _mirageStrike = false;
        }

        private void AddBossTime(double seconds, bool party = true)
        {
            if (seconds <= 0 || !_state.FightingBoss || !_state.EnemyAlive)
                return;
            var ticks = Ticks(seconds);
            if (!_state.SharedEnemy)
                _state.BossTicksLeft += ticks;
            if (party)
                S.PartyTimeTicks += (int)System.Math.Round(ticks / _rules.PartyTimeSlow);
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
