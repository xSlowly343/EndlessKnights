using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class MothcallerMechanics : IKillRewardListener
    {
        public static readonly int TimeEffect = GemDef.NetIdOf("trait_moth_time");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private MothcallerRules _rules;

        public MothcallerMechanics(MothcallerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(MothcallerRules rules) => _rules = rules;

        private MothcallerState S => _state.Mothcaller;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Mothcaller);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Swarm => On && SwarmPower > 0;
        public bool Pollen => On && PollenPower > 0;
        public bool Moth => On && MothPower > 0;
        public bool Traited => Swarm || Pollen || Moth;
        public bool Awake => Moth && S.AwakeTicks > 0;

        public double SwarmPower => Positive(MothcallerStats.SwarmPower);
        public double PollenPower => Positive(MothcallerStats.PollenPower);
        public double MothPower => Positive(MothcallerStats.MothPower);

        public int SwarmMax => System.Math.Max(1, System.Math.Min(_rules.SwarmCap, _rules.SwarmMax + (int)System.Math.Round(_hero.Value(MothcallerStats.SwarmMax, 0))));

        public double EffectiveMoths => S.Moths + (Flag(MothcallerStats.SwarmGolden) ? (S.Moths / _rules.GoldenEvery) * (_rules.GoldenMultiplier - 1) : 0);

        public double PollenRate => S.Moths * PollenPower * (Flag(MothcallerStats.PollenTwo) && !InParty ? _rules.PollenTwoShare : 1);

        public double MothEverySeconds => System.Math.Max(_rules.MothEverySecondsMin, _rules.MothEverySeconds + _hero.Value(MothcallerStats.MothEvery, 0));

        public double MothAwakeSeconds => System.Math.Max(_rules.MothAwakeSecondsMin, _rules.MothAwakeSeconds + _hero.Value(MothcallerStats.MothSeconds, 0));

        public double SecondsToWake => Moth && S.AwakeTicks <= 0 ? S.MothTicks * GameLoop.TickSeconds : 0;

        public double StrikeMultiplier() => Awake && Flag(MothcallerStats.MothStrike) ? 1 + _rules.MothStrike : 1;

        public void AfterStrike(bool crit)
        {
            BoonStrike(crit);
            if (!Swarm && !Pollen)
                return;
            S.Moths = System.Math.Min(SwarmMax, S.Moths + (Flag(MothcallerStats.SwarmDouble) ? 2 : 1));
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.Moths > 0 && (Swarm || Pollen))
                S.Moths = System.Math.Min(SwarmMax, S.Moths + strike.Moths);
            if (strike.SwarmHit > 0 && S.Moths > 0)
                SwarmHit(strike.SwarmHit * EffectiveMoths * System.Math.Max(SwarmPower, PollenPower * 10));
            if (strike.Wake && Moth)
                Wake();
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (Swarm)
                TickSwarm();
            if (Pollen)
                TickPollen();
            if (Moth)
                TickMoth();
        }

        public void OnKillRewarded()
        {
            if (!Swarm)
                return;
            S.Moths = (int)System.Math.Round(S.Moths * Carry);
        }

        private double Carry => Flag(MothcallerStats.SwarmNoCarry) ? 0 : Flag(MothcallerStats.SwarmKeep) ? _rules.SwarmKeep : _rules.SwarmCarry;

        private void TickSwarm()
        {
            if (Flag(MothcallerStats.SwarmDecay) && S.Moths > 0 && ++S.DecayTicks >= GameLoop.TicksPerSecond)
            {
                S.DecayTicks = 0;
                S.Moths--;
            }
            if (S.Moths <= 0 || !_state.EnemyAlive)
                return;
            S.SwarmCarry += _rules.SwarmHitsPerSecond * GameLoop.TickSeconds;
            while (S.SwarmCarry >= 1 - 1e-9)
            {
                S.SwarmCarry -= 1;
                var boss = _state.FightingBoss && Flag(MothcallerStats.SwarmBoss) ? _rules.SwarmBoss : 1;
                SwarmHit(EffectiveMoths * SwarmPower * boss);
            }
        }

        private void SwarmHit(double multiplier)
        {
            TraitHit(multiplier);
            if (Flag(MothcallerStats.SwarmQueue))
                Queue(multiplier * _rules.SwarmQueueShare);
        }

        private void TickPollen()
        {
            if (S.PollenRelayTicks-- > 0)
                return;
            S.PollenRelayTicks = GameLoop.TicksPerSecond - 1;
            var rate = PollenRate;
            if (!(rate > 0))
                return;
            var burn = Flag(MothcallerStats.PollenFast) ? _rules.PollenFastSeconds : _rules.PollenBurnSeconds;
            var every = Flag(MothcallerStats.PollenHeavy) ? 2 : 1;
            var queue = Flag(MothcallerStats.PollenQueue) ? 1 : 0;
            var light = Flag(MothcallerStats.PollenLight) ? _rules.PollenLight : 0;
            LandPollen(Boons, rate * (every == 2 ? 2 : 1), _rules.PollenRelaySeconds, burn, every, queue, light);
        }

        public static void LandPollen(AllyBoons boons, double rate, double seconds, double burnSeconds, int every, int queue, double light)
        {
            boons.PollenRate = rate;
            boons.PollenTicks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            boons.PollenBurnSeconds = burnSeconds;
            boons.PollenEvery = System.Math.Max(1, every);
            boons.PollenQueue = queue > 0;
            boons.PollenLight = light;
        }

        private void TickMoth()
        {
            if (S.MothTicks <= 0 && S.AwakeTicks <= 0 && S.Wakes == 0)
                S.MothTicks = Ticks(MothEverySeconds);
            if (Flag(MothcallerStats.MothBoss) && _state.FightingBoss && !S.BossSeen)
            {
                S.BossSeen = true;
                Wake();
            }
            if (S.AwakeTicks > 0)
            {
                TickAwake();
            }
            else if (S.MothTicks > 0)
            {
                S.MothTicks--;
                if (S.MothTicks <= 0)
                    Wake();
            }
        }

        private bool OwnHitting => _state.KeylessTicks < GameLoop.TicksPerSecond;

        private void TickAwake()
        {
            if (_state.EnemyAlive)
            {
                S.MothCarry += GameLoop.TickSeconds;
                while (S.MothCarry >= 1 - 1e-9)
                {
                    S.MothCarry -= 1;
                    var penalty = Flag(MothcallerStats.MothBoss) && !_state.FightingBoss ? 1 - _rules.MothNormalPenalty : 1;
                    var power = MothPower * penalty * (InParty ? 1 + Positive(MothcallerStats.MothParty) : 1);
                    TraitHit(power);
                    if (Flag(MothcallerStats.MothQueue))
                        Queue(power * _rules.MothQueueShare);
                }
            }
            if (Flag(MothcallerStats.MothNight) && OwnHitting)
                return;
            if (--S.AwakeTicks <= 0)
                S.MothTicks = Ticks(MothEverySeconds);
        }

        private void Wake()
        {
            S.Wakes++;
            S.AwakeTicks = Ticks(MothAwakeSeconds);
            S.MothTicks = 0;
            S.MothCarry = 1;
            if (!Flag(MothcallerStats.MothTime) || !_state.FightingBoss)
                return;
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(TimeEffect, 0, _rules.MothTimeSeconds, 1);
            else
                _state.BossTicksLeft += Ticks(_rules.MothTimeSeconds);
        }

        private void BoonStrike(bool crit)
        {
            if (!crit || Boons.Pollen <= 0)
                return;
            if (++Boons.PollenCrits < Boons.PollenEvery)
                return;
            Boons.PollenCrits = 0;
            Boons.PollenShakes++;
            var seconds = System.Math.Max(GameLoop.TickSeconds, Boons.PollenBurnSeconds);
            Boons.PollenBurn = (Boons.PollenBurn * Boons.PollenBurnTicks * GameLoop.TickSeconds + Boons.Pollen) / seconds;
            Boons.PollenBurnTicks = Ticks(seconds);
            Boons.Pollen = 0;
            if (Boons.PollenLight > 0)
                _state.PendingResource += Boons.PollenLight;
        }

        private void TickBoons()
        {
            if (Boons.PollenTicks > 0)
            {
                Boons.PollenTicks--;
                Boons.Pollen = System.Math.Min(_rules.PollenMax, Boons.Pollen + Boons.PollenRate * GameLoop.TickSeconds);
            }
            if (Boons.PollenBurnTicks > 0)
            {
                Boons.PollenBurnTicks--;
                Boons.PollenBurnCarry += GameLoop.TickSeconds;
                if (Boons.PollenBurnCarry >= 1 - 1e-9 || Boons.PollenBurnTicks == 0)
                {
                    var power = Boons.PollenBurn * Boons.PollenBurnCarry;
                    Boons.PollenBurnCarry = 0;
                    TraitHit(power, StatTags.DoT);
                    if (Boons.PollenQueue)
                        Queue(power);
                }
            }
        }

        private void Queue(double multiplier) =>
            _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard));

        private void TraitHit(double multiplier, StatTags extra = StatTags.None)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard | extra);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
