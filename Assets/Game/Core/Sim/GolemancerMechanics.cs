using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GolemancerMechanics
    {
        public static readonly int BrandEffect = GemDef.NetIdOf("trait_golem_brand");
        public static readonly int GuardEffect = GemDef.NetIdOf("trait_golem_guard");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private GolemancerRules _rules;

        public GolemancerMechanics(GolemancerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(GolemancerRules rules) => _rules = rules;

        private GolemancerState S => _state.Golemancer;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Golemancer);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Seal => On && SealPower > 0;
        public bool Eternal => On && EternalPower > 0;
        public bool Minor => On && MinorPower > 0;
        public bool Traited => Seal || Eternal || Minor;
        public bool GolemStanding => S.GolemTicks > 0 || S.SecondGolemTicks > 0;

        public double SealPower => Positive(GolemancerStats.SealPower);
        public double EternalPower => Positive(GolemancerStats.EternalPower);
        public double MinorPower => Positive(GolemancerStats.MinorPower);

        public int SealHits => System.Math.Max(_rules.SealHitsMin, (int)System.Math.Round(_rules.SealHits + _hero.Value(GolemancerStats.SealHits, 0)));

        public double GolemSeconds => Flag(GolemancerStats.SealTitan) ? _rules.TitanSeconds : System.Math.Max(1, _rules.GolemSeconds + _hero.Value(GolemancerStats.SealSeconds, 0));

        public int RuneHits => System.Math.Max(_rules.RuneHitsMin, (int)System.Math.Round(_rules.RuneHits + _hero.Value(GolemancerStats.EternalRuneHits, 0)));

        public int RunesMax => System.Math.Max(1, System.Math.Min(_rules.RunesCap, _rules.RunesMax + (int)System.Math.Round(_hero.Value(GolemancerStats.EternalRunes, 0))));

        public double RuneBonus => _rules.RuneBonus + _hero.Value(GolemancerStats.EternalRuneBonus, 0);

        public int BrandMax => System.Math.Max(1, _rules.BrandMax + (int)System.Math.Round(_hero.Value(GolemancerStats.EternalBrand, 0)));

        public double EternalStrength => EternalPower * (1 + S.Runes * RuneBonus) * BossFactor;

        private double BossFactor => !Flag(GolemancerStats.EternalBoss) ? 1 : _state.FightingBoss ? _rules.BossMultiplier : 1 - _rules.NormalPenalty;

        public int MinorHits => System.Math.Max(_rules.MinorHitsMin, (int)System.Math.Round(_rules.MinorHits + _hero.Value(GolemancerStats.MinorHits, 0)));

        public int MinorMax => System.Math.Max(1, System.Math.Min(System.Math.Min(_rules.MinorCap, GolemancerState.Capacity), _rules.MinorMax + (int)System.Math.Round(_hero.Value(GolemancerStats.MinorMax, 0))));

        public double MinorStrength => MinorPower * (Flag(GolemancerStats.MinorSolo) && !InParty ? 2 : 1);

        public void AfterStrike()
        {
            BoonStrike();
            if (!On)
                return;
            if (Seal && !GolemStanding && ++S.SealHits >= SealHits)
            {
                S.SealHits = 0;
                RaiseGolem();
            }
            if (Eternal)
            {
                if (++S.RuneHits >= RuneHits)
                {
                    S.RuneHits = 0;
                    AddRunes(1);
                }
                if (!InParty)
                    AddBrandHits(_rules.BrandSoloShare);
            }
            if (Minor && ++S.MinorHits >= MinorHits)
            {
                S.MinorHits = 0;
                Shape(1);
            }
        }

        public void AddAllyHits(double hits)
        {
            if (Eternal && hits > 0)
                AddBrandHits(hits);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.SealHits > 0)
            {
                if (Seal && !GolemStanding)
                {
                    S.SealHits += strike.SealHits;
                    if (S.SealHits >= SealHits)
                    {
                        S.SealHits = 0;
                        RaiseGolem();
                    }
                }
                if (Eternal)
                {
                    S.RuneHits += strike.SealHits;
                    while (S.RuneHits >= RuneHits)
                    {
                        S.RuneHits -= RuneHits;
                        AddRunes(1);
                    }
                }
                if (Minor)
                {
                    S.MinorHits += strike.SealHits;
                    while (S.MinorHits >= MinorHits)
                    {
                        S.MinorHits -= MinorHits;
                        Shape(1);
                    }
                }
            }
            if (strike.CompleteSeal && Seal && !GolemStanding)
            {
                S.SealHits = 0;
                RaiseGolem();
            }
            if (strike.GolemRunes > 0 && Eternal)
                AddRunes(strike.GolemRunes);
            if (strike.Golemlings > 0 && Minor)
                Shape(strike.Golemlings);
            if (strike.GolemSeconds > 0 && S.GolemTicks > 0)
                S.GolemTicks += Ticks(strike.GolemSeconds);
            if (strike.GolemHit > 0)
                GolemHit(strike.GolemHit);
        }

        public void ReceiveCorner(int slot)
        {
            if (!Seal || slot < 0 || slot >= 8)
                return;
            S.CornersReceived++;
            S.Corners |= 1 << slot;
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (S.Spawned)
            {
                S.Spawned = false;
                S.Brand = Eternal ? System.Math.Min(BrandMax, S.Runes) : 0;
                S.BrandResendTicks = 0;
            }
            if (Seal)
                TickGolem();
            if (Eternal)
                TickEternal();
            if (Minor)
                TickMinors();
        }

        private int CornerCount()
        {
            var count = 0;
            for (var slot = 0; slot < 8; slot++)
            {
                if ((S.Corners & (1 << slot)) != 0)
                    count++;
            }
            return count;
        }

        private void RaiseGolem()
        {
            S.Seals++;
            var power = SealPower * (1 + CornerCount() * (_rules.CornerBonus + Positive(GolemancerStats.SealParty))) * (Flag(GolemancerStats.SealTitan) ? 2 : 1);
            S.Corners = 0;
            S.GolemPower = power;
            S.GolemTicks = Ticks(GolemSeconds);
            S.GolemCarry = 1;
            if (Flag(GolemancerStats.SealTwo))
                S.SecondGolemTicks = S.GolemTicks;
            if (!Flag(GolemancerStats.SealGuard) || !_state.FightingBoss)
                return;
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(GuardEffect, 0, _rules.GuardStopSeconds, 1);
            else
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, Ticks(_rules.GuardStopSeconds));
        }

        private void TickGolem()
        {
            if (GolemStanding && _state.EnemyAlive)
            {
                S.GolemCarry += GameLoop.TickSeconds;
                while (S.GolemCarry >= 1 - 1e-9)
                {
                    S.GolemCarry -= 1;
                    var golems = (S.GolemTicks > 0 ? 1 : 0) + (S.SecondGolemTicks > 0 ? 1 : 0);
                    var share = Flag(GolemancerStats.SealTwo) ? _rules.TwoShare : 1;
                    var crystal = Flag(GolemancerStats.SealCrystal);
                    var power = S.GolemPower * golems * share * (crystal ? 1 - _rules.CrystalPenalty : 1);
                    if (crystal)
                        TraitHit(power, 1);
                    else
                        TraitHit(power);
                }
            }
            if (S.SecondGolemTicks > 0)
                S.SecondGolemTicks--;
            if (S.GolemTicks > 0 && --S.GolemTicks == 0 && Flag(GolemancerStats.SealCrumble))
                Queue(S.GolemPower * _rules.CrumbleMultiplier);
            OfferCorners();
        }

        private void OfferCorners()
        {
            if (!InParty || S.CornerOfferTicks-- > 0)
                return;
            S.CornerOfferTicks = Ticks(_rules.CornerOfferSeconds) - 1;
            _state.TraitRelays.Add(new TraitRelay
            {
                Kind = TraitRelayKind.Corner,
                To = TraitRelay.AllSlots,
                Every = (byte)_rules.CornerHits,
                Count = (byte)(S.Seals & 0xFF),
                Seconds = _rules.CornerOfferSeconds * 2
            });
        }

        private void GolemHit(double multiplier)
        {
            if (Seal && GolemStanding)
                TraitHit(S.GolemPower * multiplier);
            if (Eternal)
                TraitHit(EternalStrength * multiplier);
            if (Minor && S.Minors > 0)
                TraitHit(MinorStrength * S.Minors * multiplier);
        }

        private void AddRunes(int count) => S.Runes = System.Math.Min(RunesMax, S.Runes + count);

        private void TickEternal()
        {
            if (_state.EnemyAlive)
            {
                var every = Flag(GolemancerStats.EternalHeavy) ? _rules.HeavyEverySeconds : 1;
                S.EternalCarry += GameLoop.TickSeconds / every;
                while (S.EternalCarry >= 1 - 1e-9)
                {
                    S.EternalCarry -= 1;
                    var power = EternalStrength * (Flag(GolemancerStats.EternalHeavy) ? _rules.HeavyMultiplier : 1);
                    TraitHit(power);
                    if (Flag(GolemancerStats.EternalQueue))
                        Queue(power * _rules.EternalQueueShare);
                }
            }
            if (!InParty || S.Brand <= 0 || S.BrandResendTicks-- > 0)
                return;
            S.BrandResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(BrandEffect, 0, _rules.BrandEffectSeconds);
        }

        private void AddBrandHits(double hits)
        {
            if (S.Brand <= 0)
                return;
            S.BrandHits += hits;
            while (S.BrandHits >= _rules.BrandHits && S.Brand > 0)
            {
                S.BrandHits -= _rules.BrandHits;
                S.Brand--;
                S.BrandPops++;
                TraitHit(EternalStrength * _rules.BrandMultiplier * (1 + Positive(GolemancerStats.EternalParty)));
            }
            if (S.Brand == 0)
                S.BrandHits = 0;
        }

        private void Shape(int count)
        {
            for (var n = 0; n < count; n++)
            {
                if (InParty)
                {
                    S.MinorsSent++;
                    _state.TraitRelays.Add(new TraitRelay
                    {
                        Kind = TraitRelayKind.Golemling,
                        To = LanternbearerMechanics.AnyAlly,
                        Value = MinorStrength * System.Math.Max(0, 1 + _hero.Value(GolemancerStats.MinorParty, 0)),
                        Seconds = MinorSeconds
                    });
                    continue;
                }
                var index = -1;
                for (var i = 0; i < MinorMax; i++)
                {
                    if (S.MinorTicks[i] <= 0)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                    index = Oldest();
                S.MinorTicks[index] = Ticks(MinorSeconds);
                if (Flag(GolemancerStats.MinorBirth))
                    TraitHit(MinorStrength * _rules.MinorBirth);
            }
            CountMinors();
        }

        private double MinorSeconds => System.Math.Max(1, _rules.MinorSeconds + _hero.Value(GolemancerStats.MinorSeconds, 0));

        private int Oldest()
        {
            var oldest = 0;
            for (var i = 1; i < MinorMax; i++)
            {
                if (S.MinorTicks[i] < S.MinorTicks[oldest])
                    oldest = i;
            }
            return oldest;
        }

        private void CountMinors()
        {
            var count = 0;
            for (var i = 0; i < GolemancerState.Capacity; i++)
            {
                if (S.MinorTicks[i] > 0)
                    count++;
            }
            S.Minors = count;
        }

        private void TickMinors()
        {
            for (var i = 0; i < GolemancerState.Capacity; i++)
            {
                if (S.MinorTicks[i] > 0)
                    S.MinorTicks[i]--;
            }
            CountMinors();
            if (S.Minors <= 0 || !_state.EnemyAlive)
                return;
            S.MinorCarry += GameLoop.TickSeconds;
            if (S.MinorCarry < 1 - 1e-9)
                return;
            S.MinorCarry -= 1;
            var power = MinorStrength * S.Minors;
            TraitHit(power);
            if (Flag(GolemancerStats.MinorQueue))
                Queue(power * _rules.MinorQueueShare);
        }

        private void BoonStrike()
        {
            if (Boons.CornerTicks <= 0 || Boons.CornerOwner < 0 || Boons.CornerDone)
                return;
            if (++Boons.CornerHits < System.Math.Max(1, Boons.CornerEvery))
                return;
            Boons.CornerDone = true;
            Boons.CornersWritten++;
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.CornerDone, To = (byte)Boons.CornerOwner });
        }

        public static void OfferCorner(AllyBoons boons, int owner, int every, int seal, double seconds)
        {
            if (boons.CornerSeal != seal || boons.CornerOwner != owner)
            {
                boons.CornerSeal = seal;
                boons.CornerDone = false;
                boons.CornerHits = 0;
            }
            boons.CornerOwner = owner;
            boons.CornerEvery = every;
            boons.CornerTicks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
        }

        public static void LandGolemling(AllyBoons boons, double power, double seconds)
        {
            boons.Golemlings = boons.GolemlingTicks > 0 ? System.Math.Min(4, boons.Golemlings + 1) : 1;
            boons.GolemlingPower = power;
            boons.GolemlingTicks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
        }

        private void TickBoons()
        {
            if (Boons.CornerTicks > 0)
                Boons.CornerTicks--;
            if (Boons.GolemlingTicks <= 0)
                return;
            if (--Boons.GolemlingTicks == 0)
                Boons.Golemlings = 0;
            if (!_state.EnemyAlive || !(Boons.GolemlingPower > 0))
                return;
            Boons.GolemlingCarry += GameLoop.TickSeconds;
            if (Boons.GolemlingCarry < 1 - 1e-9)
                return;
            Boons.GolemlingCarry -= 1;
            for (var i = 0; i < Boons.Golemlings; i++)
            {
                Boons.GolemlingHits++;
                TraitHit(Boons.GolemlingPower);
                _combat.ExtraStrike();
            }
        }

        private void Queue(double multiplier) =>
            _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard));

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
