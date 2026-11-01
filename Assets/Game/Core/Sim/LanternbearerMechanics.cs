using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class LanternbearerMechanics
    {
        public const byte AnyAlly = 254;
        public static readonly int LightEffect = GemDef.NetIdOf("trait_lantern_light");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private LanternbearerRules _rules;
        private int _lightResendTicks;

        public LanternbearerMechanics(LanternbearerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(LanternbearerRules rules) => _rules = rules;

        private LanternbearerState S => _state.Lanternbearer;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Lanternbearer);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Light => On && LightPower > 0;
        public bool Fireflies => On && SparkPower > 0;
        public bool Beacon => On && BeaconPower > 0;

        public double LightPower => Positive(LanternbearerStats.LightPower);
        public double SparkPower => Positive(LanternbearerStats.SparkPower);
        public double BeaconPower => Positive(LanternbearerStats.BeaconPower);

        public double LightPerHit => System.Math.Max(_rules.LightPerHitMin, _rules.LightPerHit + _hero.Value(LanternbearerStats.LightPerHit, 0));

        public int ShadowTicks => Ticks(System.Math.Max(_rules.ShadowSecondsMin, _rules.ShadowSeconds + _hero.Value(LanternbearerStats.LightShadowSeconds, 0)));

        public double ShadowShare => System.Math.Min(1, System.Math.Max(0, _rules.ShadowShare + _hero.Value(LanternbearerStats.LightShadow, 0)));

        public double LightBonus => !Light || !S.LitFull ? 0 : LightPower * (S.FlareTicks > 0 ? _rules.FlareMultiplier : 1);

        public double LightStrength => LightBonus + (Light && S.LitFull ? Positive(LanternbearerStats.LightParty) : 0);

        public int FireflyHits => System.Math.Max(_rules.FireflyHitsMin, (int)System.Math.Round(_rules.FireflyHits + _hero.Value(LanternbearerStats.FireflyHits, 0)));

        public int FireflyTicks => Ticks(System.Math.Max(_rules.FireflySecondsMin, _rules.FireflySeconds + _hero.Value(LanternbearerStats.FireflySeconds, 0)));

        public int FireflyMax => System.Math.Max(1, (int)System.Math.Round(_rules.FireflyMax + _hero.Value(LanternbearerStats.FireflyMax, 0)));

        public double SparkOil => System.Math.Max(0, _rules.SparkOil + _hero.Value(LanternbearerStats.SparkOil, 0));

        public int BeaconTicks => Ticks(System.Math.Max(_rules.BeaconSecondsMin, _rules.BeaconSeconds + _hero.Value(LanternbearerStats.BeaconSeconds, 0)));

        public int BeaconCharges => System.Math.Max(1, 1 + (int)System.Math.Round(_hero.Value(LanternbearerStats.BeaconCharges, 0)));

        public double BeaconStrength => BeaconPower + Positive(LanternbearerStats.BeaconParty);

        public double DamageTaken()
        {
            if (!On || _state.SharedEnemy)
                return 1;
            return 1 + LightBonus;
        }

        public bool Forbids(GemDef skill) => Beacon && Flag(LanternbearerStats.BeaconMute) && !(skill.Skill?.Effect is AuraEffect);

        public void AddAllyHits(double hits)
        {
            if (Light && hits > 0)
                AddLight(hits * LightPerHit);
        }

        public void AfterStrike(bool crit)
        {
            StrikeBoons(crit);
            if (!On)
                return;
            if (Light)
                AddLight(LightPerHit);
            if (!Fireflies || ++S.FireflyHits < FireflyHits)
                return;
            S.FireflyHits = 0;
            SendFireflies(1);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.LitHit > 0 && Light && S.Light > 0)
                TraitHit(strike.LitHit * S.Light);
            if (strike.Oil > 0)
                _state.PendingResource += strike.Oil;
            if (strike.Light > 0 && Light)
                AddLight(strike.Light);
            if (strike.Fireflies > 0 && Fireflies)
                SendFireflies(strike.Fireflies);
            if (strike.Flash && Beacon)
                Flash();
            if (strike.GlowSeconds > 0 && strike.GlowPerOil > 0)
                Glow(strike.GlowSeconds, strike.GlowPerOil);
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (S.SpawnPending)
                OnNewEnemy();
            if (Light)
                TickLight();
            else
                ClearLight();
            if (Beacon && ++S.BeaconTicks >= BeaconTicks)
                Flash();
            if (!Beacon)
                S.BeaconTicks = 0;
            if (S.StrikeTicks > 0)
                S.StrikeTicks--;
            if (S.GlowTicks > 0 && --S.GlowTicks == 0)
                S.GlowPower = 0;
            _state.TraitPartyDamage = S.GlowPower + (S.StrikeTicks > 0 ? _rules.BeaconStrike : 0);
            _state.TraitLight = Light ? S.Light : -1;
            ShareLight();
        }

        public void ReceiveSparks(int sparks)
        {
            if (On && Fireflies && sparks > 0)
                _state.PendingResource += sparks * SparkOil;
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            S.Light = Light ? S.Light * System.Math.Min(1, Positive(LanternbearerStats.LightCarry)) : 0;
            S.LitFull = false;
            S.LitOilGiven = false;
            S.FlareTicks = 0;
            S.ShadowTicks = 0;
            if (Beacon && _state.FightingBoss && Flag(LanternbearerStats.BeaconBoss))
                Flash();
        }

        private void AddLight(double amount)
        {
            if (amount <= 0 || !_state.EnemyAlive)
                return;
            S.Light = System.Math.Min(1, S.Light + amount);
            if (S.LitFull || S.Light < 1)
                return;
            S.LitFull = true;
            S.FullLights++;
            _lightResendTicks = 0;
            if (Flag(LanternbearerStats.LightFlare))
                S.FlareTicks = Ticks(_rules.FlareSeconds);
            if (!Flag(LanternbearerStats.LightOil) || S.LitOilGiven)
                return;
            S.LitOilGiven = true;
            _state.PendingResource += _rules.LightOil;
            _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, _rules.LightOil);
        }

        private void TickLight()
        {
            if (S.FlareTicks > 0 && --S.FlareTicks == 0)
                Dim(1 - _rules.FlareKeep);
            if (++S.ShadowTicks < ShadowTicks)
                return;
            S.ShadowTicks = 0;
            if (Flag(LanternbearerStats.LightBoss) && _state.FightingBoss)
                return;
            if (Flag(LanternbearerStats.LightNoon) && S.LitFull)
                return;
            Dim(ShadowShare);
        }

        private void Dim(double share)
        {
            S.Light = System.Math.Max(0, S.Light - share);
            if (S.Light < 1)
                S.LitFull = false;
        }

        private void ClearLight()
        {
            S.Light = 0;
            S.LitFull = false;
            S.FlareTicks = 0;
        }

        private void ShareLight()
        {
            if (!InParty || !S.LitFull || LightStrength <= 0)
                return;
            if (_lightResendTicks-- > 0)
                return;
            _lightResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(LightEffect, LightStrength, _rules.LightEffectSeconds);
        }

        private void SendFireflies(int count)
        {
            S.FirefliesSent += count;
            var every = Flag(LanternbearerStats.SparkEvery) ? _rules.SparkEvery : 0;
            var burst = Flag(LanternbearerStats.FireflyBurst) ? _rules.BurstMultiplier : 0;
            if (!InParty)
            {
                Boons.Land(count, FireflyTicks, SparkPower, every, burst, -1, FireflyMax);
                return;
            }
            _state.TraitRelays.Add(new TraitRelay
            {
                Kind = TraitRelayKind.Firefly,
                To = AnyAlly,
                Count = (byte)System.Math.Min(byte.MaxValue, count),
                Every = (byte)System.Math.Min(byte.MaxValue, every),
                Max = (byte)System.Math.Min(byte.MaxValue, FireflyMax),
                Value = SparkPower,
                Seconds = FireflyTicks * GameLoop.TickSeconds,
                Extra = burst
            });
        }

        private void Flash()
        {
            S.BeaconTicks = 0;
            S.Flashes++;
            var cut = Positive(LanternbearerStats.BeaconCut);
            Boons.Flash(BeaconCharges, BeaconStrength);
            CutCooldowns(cut);
            if (Flag(LanternbearerStats.BeaconStrike))
                S.StrikeTicks = Ticks(_rules.BeaconStrikeSeconds);
            if (!InParty)
                return;
            _state.TraitRelays.Add(new TraitRelay
            {
                Kind = TraitRelayKind.Beacon,
                To = TraitRelay.AllSlots,
                Count = (byte)BeaconCharges,
                Value = BeaconStrength,
                Seconds = cut
            });
        }

        public void CutCooldowns(double seconds)
        {
            var cut = Ticks(seconds);
            if (cut <= 0)
                return;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                var slot = _state.Skills[i];
                if (slot.CooldownTicks > 1)
                    slot.CooldownTicks = System.Math.Max(1, slot.CooldownTicks - cut);
            }
        }

        private void Glow(double seconds, double perOil)
        {
            var oil = _state.Resource;
            if (oil <= 0)
                return;
            _state.Resource = 0;
            S.GlowPower = System.Math.Max(S.GlowPower, oil * perOil);
            S.GlowTicks = System.Math.Max(S.GlowTicks, Ticks(seconds));
        }

        private void TickBoons()
        {
            if (Boons.Fireflies <= 0 || Boons.FireflyTicks <= 0 || --Boons.FireflyTicks > 0)
                return;
            if (Boons.BurstPower > 0)
                TraitHit(Boons.BurstPower * Boons.Fireflies);
            Boons.Fireflies = 0;
        }

        private void StrikeBoons(bool crit)
        {
            if (Boons.Fireflies <= 0)
                return;
            var spark = crit;
            if (Boons.SparkEvery > 0 && ++Boons.StrikesSinceSpark >= Boons.SparkEvery)
            {
                Boons.StrikesSinceSpark = 0;
                spark = true;
            }
            if (!spark)
                return;
            Boons.Sparks++;
            TraitHit(Boons.SparkPower);
            if (Boons.FireflyOwner < 0)
                ReceiveSparks(1);
            else
                Boons.SparksOwed++;
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
