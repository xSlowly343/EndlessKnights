using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ClockwrightMechanics
    {
        public static readonly int LoopEffect = GemDef.NetIdOf("trait_clock_loop");
        public static readonly int LoopStopEffect = GemDef.NetIdOf("trait_clock_loop_stop");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private ClockwrightRules _rules;

        public ClockwrightMechanics(ClockwrightRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(ClockwrightRules rules) => _rules = rules;

        private ClockwrightState S => _state.Clockwright;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Clockwright);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Spring => On && SpringPower > 0;
        public bool Loop => On && LoopPower > 0;
        public bool Gears => On && GearPower > 0;

        public double SpringPower => Positive(ClockwrightStats.SpringPower);
        public double LoopPower => Positive(ClockwrightStats.LoopPower);
        public double GearPower => Positive(ClockwrightStats.GearPower);

        public double Wind => System.Math.Min(1, System.Math.Max(0, _state.ResourceFill));

        public double SpringSlow
        {
            get
            {
                if (!Spring)
                    return 0;
                if (_state.Resource <= 1e-9)
                    return Flag(ClockwrightStats.SpringForever) ? 0 : -_rules.SpringPenalty;
                if (Wind < 0.5)
                    return Flag(ClockwrightStats.SpringTight) ? 0 : _rules.SpringLow;
                var full = Flag(ClockwrightStats.SpringFull) && Wind >= _rules.SpringFullAbove ? _rules.SpringFullSlow : 0;
                return System.Math.Min(0.9, SpringPower + full);
            }
        }

        public double SpringDrain => _rules.SpringDrainPerSecond * System.Math.Max(0, 1 + _hero.Value(ClockwrightStats.SpringDrain, 0))
            * (Flag(ClockwrightStats.SpringFast) ? 2 : 1) * (Flag(ClockwrightStats.SpringAllies) ? 1.5 : 1);

        public int LoopTicks
        {
            get
            {
                var seconds = System.Math.Max(_rules.LoopEverySecondsMin, _rules.LoopEverySeconds + _hero.Value(ClockwrightStats.LoopSeconds, 0));
                if (Flag(ClockwrightStats.LoopBoss) && _state.FightingBoss)
                    seconds *= 0.5;
                return Ticks(seconds);
            }
        }

        public double LoopWindow => System.Math.Min(_rules.LoopWindowMax, System.Math.Max(0.5, _rules.LoopWindowSeconds + _hero.Value(ClockwrightStats.LoopWindow, 0)));

        public double LoopShare => LoopPower * (Flag(ClockwrightStats.LoopTwice) ? 1 + _rules.LoopTwice : 1);

        public int GearHits => System.Math.Max(_rules.GearHitsMin, (int)System.Math.Round(_rules.GearHits + _hero.Value(ClockwrightStats.GearHits, 0)));

        public double OwnCooldownMultiplier => Gears ? (Flag(ClockwrightStats.GearOwn) ? _rules.GearOwnSoft : _rules.GearOwn) : 1;

        public double StrikeMultiplier() => Spring && Flag(ClockwrightStats.SpringStrike) && Wind >= 0.5 ? 1 + _rules.SpringStrike : 1;

        public void AddAllyHits(double hits)
        {
            if (Spring && hits > 0)
                _state.PendingResource += hits * _rules.SpringAllyWind * (Flag(ClockwrightStats.SpringAllies) ? 2 : 1);
        }

        public void AfterStrike()
        {
            if (!On)
                return;
            if (Spring && Flag(ClockwrightStats.SpringFast))
                _state.PendingResource += _rules.SpringWindPerHit;
            if (Gears && ++S.GearHits >= GearHits)
            {
                S.GearHits = 0;
                TickGears(GearPower);
            }
        }

        public void OnSkillCast(GemDef skill)
        {
            ReportCog();
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (Gears && !(skill.Skill.Effect is AuraEffect))
                NoteOwnCast();
            if (strike.Wind > 0)
                _state.PendingResource += strike.Wind;
            if (strike.WindHit > 0)
                TraitHit(strike.WindHit * Wind);
            if (strike.HoldSeconds > 0)
                S.HoldTicks = System.Math.Max(S.HoldTicks, Ticks(strike.HoldSeconds));
            if (strike.CutCooldowns > 0)
                ShareCut(strike.CutCooldowns, false);
            if (strike.WindBurst > 0)
                WindBurst(strike.WindBurst);
            if (strike.Rewind && Loop)
                Rewind();
            if (strike.GearTick && Gears)
                TickGears(GearPower);
            if (strike.BossTimerStopSeconds > 0 && _state.FightingBoss && !_state.SharedEnemy)
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, Ticks(strike.BossTimerStopSeconds));
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (Spring)
                TickSpring();
            if (Loop && ++S.LoopTicks >= LoopTicks && LoopAllowed)
                Rewind();
            if (Gears)
                TickIdleGears();
            if (Gears && InParty && S.CallTicks-- <= 0)
                CallGears();
            var slow = SpringSlow;
            _state.TraitTimerSlow = _state.SharedEnemy ? 0 : slow;
            if (Spring)
                _state.TraitPartyTimerSlow = slow > 0 ? slow + Positive(ClockwrightStats.SpringParty) : 0;
        }

        public void ReceiveCog(int slot)
        {
            if (!Gears || slot < 0 || slot >= ClockwrightState.Slots)
                return;
            S.CogTicks[slot] = _state.Tick;
            TryMesh();
        }

        public void CutCooldowns(double seconds, bool reset)
        {
            var cut = Ticks(seconds);
            var longest = -1;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                var slot = _state.Skills[i];
                if (slot.CooldownTicks > 1 && cut > 0)
                    slot.CooldownTicks = System.Math.Max(1, slot.CooldownTicks - cut);
                if (slot.CooldownTicks > 1 && (longest < 0 || slot.CooldownTicks > _state.Skills[longest].CooldownTicks))
                    longest = i;
            }
            if (reset && longest >= 0)
                _state.Skills[longest].CooldownTicks = 1;
        }

        private bool LoopAllowed => !Flag(ClockwrightStats.LoopBoss) || _state.FightingBoss;

        private void TickSpring()
        {
            if (S.HoldTicks > 0)
                S.HoldTicks--;
            else if (_state.InputStreakTicks > 0 && _state.Resource > 0)
                _state.Resource = System.Math.Max(0, _state.Resource - SpringDrain * GameLoop.TickSeconds);
            if (Flag(ClockwrightStats.SpringBurst) && ++S.BurstTicks >= Ticks(_rules.SpringBurstEverySeconds))
            {
                S.BurstTicks = 0;
                if (Wind >= 0.5)
                    WindBurst(_rules.SpringBurstPerWind);
            }
            ShareSpring();
        }

        private void ShareSpring()
        {
            if (!InParty)
                return;
            var wind = Wind;
            if (S.SpringRelayTicks-- > 0 && System.Math.Abs(wind - S.SentWind) < 0.05)
                return;
            S.SpringRelayTicks = Ticks(_rules.SpringRelaySeconds / 3);
            S.SentWind = wind;
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Spring, To = TraitRelay.AllSlots, Value = wind, Seconds = _rules.SpringRelaySeconds, Extra = SpringSlow });
        }

        private void WindBurst(double perPoint)
        {
            var wind = _state.Resource;
            if (wind <= 0)
                return;
            S.Bursts++;
            _state.Resource = wind * 0.5;
            TraitHit(wind * perPoint);
        }

        private void Rewind()
        {
            S.LoopTicks = 0;
            S.Loops++;
            var window = LoopWindow;
            if (!InParty)
            {
                var recent = _state.DamageWindow.Last(_state.TotalDamage, Ticks(window));
                _combat.DirectHit(recent * (LoopShare * _rules.LoopSolo));
            }
            else
            {
                _state.TraitLoopSeconds = window;
                _state.TraitLoopShare = LoopShare + Positive(ClockwrightStats.LoopParty);
                if (Flag(ClockwrightStats.LoopOwn))
                    _combat.DirectHit(_state.DamageWindow.Last(_state.TotalDamage, Ticks(window)) * (LoopShare * _rules.LoopSolo));
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Loop, To = TraitRelay.AllSlots, Value = _state.TraitLoopShare, Seconds = window });
            }
            if (!Flag(ClockwrightStats.LoopStop) || !_state.FightingBoss)
                return;
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(LoopStopEffect, 0, _rules.LoopStopSeconds, 1);
            else
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, Ticks(_rules.LoopStopSeconds));
        }

        private void TickGears(double cut)
        {
            S.GearTicks++;
            var reset = Flag(ClockwrightStats.GearReset) && S.GearTicks % _rules.GearResetEvery == 0;
            if (Flag(ClockwrightStats.GearResource))
            {
                _state.PendingResource += _rules.GearResource;
                _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, _rules.GearResource);
            }
            CutCooldowns(cut, reset);
            if (InParty)
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.GearTick, To = TraitRelay.AllSlots, Seconds = cut + Positive(ClockwrightStats.GearParty), Extra = reset ? 1 : 0 });
        }

        private void ShareCut(double seconds, bool reset)
        {
            CutCooldowns(seconds, reset);
            if (InParty)
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.GearTick, To = TraitRelay.AllSlots, Seconds = seconds, Extra = reset ? 1 : 0 });
        }

        private void TickIdleGears()
        {
            if (!Flag(ClockwrightStats.GearIdle) || _state.InputStreakTicks > 0)
            {
                S.IdleTicks = 0;
                return;
            }
            if (++S.IdleTicks < Ticks(_rules.GearIdleSeconds))
                return;
            S.IdleTicks = 0;
            TickGears(GearPower);
        }

        private void CallGears()
        {
            S.CallTicks = Ticks(_rules.GearCallSeconds / 3);
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Gears, To = TraitRelay.AllSlots, Seconds = _rules.GearCallSeconds });
        }

        private void NoteOwnCast()
        {
            S.PreviousOwnCastTick = S.OwnCastTick;
            S.OwnCastTick = _state.Tick;
            TryMesh();
        }

        private void TryMesh()
        {
            var window = Ticks(_rules.GearMeshSeconds);
            var now = _state.Tick;
            if (now - S.LastMeshTick < Ticks(_rules.GearMeshCooldownSeconds))
                return;
            var casters = now - S.OwnCastTick <= window ? 1 : 0;
            for (var i = 0; i < ClockwrightState.Slots; i++)
            {
                if (now - S.CogTicks[i] <= window)
                    casters++;
            }
            if (!InParty && now - S.PreviousOwnCastTick <= window && casters > 0)
                casters++;
            if (casters < 2)
                return;
            S.LastMeshTick = now;
            S.Meshes++;
            S.OwnCastTick = long.MinValue / 2;
            S.PreviousOwnCastTick = long.MinValue / 2;
            for (var i = 0; i < ClockwrightState.Slots; i++)
                S.CogTicks[i] = long.MinValue / 2;
            TickGears(GearPower * (Flag(ClockwrightStats.GearMesh) ? 2 : 1));
        }

        private void ReportCog()
        {
            if (Boons.GearTicks <= 0 || Boons.GearOwner < 0)
                return;
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Cog, To = (byte)Boons.GearOwner, Count = 1 });
        }

        private void TickBoons()
        {
            if (Boons.GearTicks > 0)
                Boons.GearTicks--;
            if (Boons.SpringTicks > 0)
                Boons.SpringTicks--;
            if (Boons.LoopTicks > 0)
                Boons.LoopTicks--;
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
