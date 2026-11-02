using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class AlchemistMechanics
    {
        public static readonly int MixEffect = GemDef.NetIdOf("trait_alch_mix");
        public static readonly int SolventEffect = GemDef.NetIdOf("trait_alch_solvent");
        public static readonly int StopEffect = GemDef.NetIdOf("trait_alch_stop");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private AlchemistRules _rules;

        public AlchemistMechanics(AlchemistRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(AlchemistRules rules) => _rules = rules;

        private AlchemistState S => _state.Alchemist;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Alchemist);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Mix => On && MixPower > 0;
        public bool Solvent => On && SolventPower > 0;
        public bool Elixirs => On && ElixirPower > 0;

        public double MixPower => Positive(AlchemistStats.MixPower);
        public double SolventPower => Positive(AlchemistStats.SolventPower);
        public double ElixirPower => Positive(AlchemistStats.ElixirPower);

        public int ReagentHits => System.Math.Max(_rules.ReagentHitsMin, (int)System.Math.Round(_rules.ReagentHits + _hero.Value(AlchemistStats.ReagentHits, 0)));

        public int LayerHits => System.Math.Max(_rules.LayerHitsMin, (int)System.Math.Round(_rules.LayerHits + _hero.Value(AlchemistStats.LayerHits, 0)));

        public int LayersOff => Solvent && S.LayersLeft >= 0 ? _rules.Layers - S.LayersLeft : 0;

        public double SolventDamageTaken => !Solvent ? 0
            : (SolventPower + Positive(AlchemistStats.SolventParty)) * LayersOff + (Flag(AlchemistStats.SolventCore) && S.LayersLeft == 0 ? _rules.CoreBonus : 0);

        public int ElixirHits => System.Math.Max(_rules.ElixirHitsMin, (int)System.Math.Round(_rules.ElixirHits + _hero.Value(AlchemistStats.ElixirHits, 0)));

        public double ElixirSeconds => System.Math.Max(1, _rules.ElixirSeconds + _hero.Value(AlchemistStats.ElixirSeconds, 0));

        public double MixShare => S.MixTicks > 0 ? S.MixDamageTaken : 0;

        public double StrikeMultiplier() => Elixirs && !Flag(AlchemistStats.ElixirNoPenalty) ? 1 - _rules.SelfPenalty : 1;

        public double DamageTaken()
        {
            if (!On || _state.SharedEnemy)
                return 1;
            return (1 + MixShare) * (1 + SolventDamageTaken);
        }

        public void AfterStrike(bool crit)
        {
            StrikeBoons(crit);
            if (!On)
                return;
            if (Mix)
                MixStrike(crit);
            if (Solvent)
                Dissolve(Boons.AcidTicks > 0 && Boons.AcidOwner < 0 ? 1 + _rules.AcidSoloShare : 1);
            if (Elixirs && ++S.ElixirHits >= ElixirHits)
            {
                S.ElixirHits = 0;
                SendElixir();
            }
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (Solvent && Flag(AlchemistStats.SolventSkills))
                Dissolve(_rules.SkillLayerHits);
            if (strike.Reagents > 0 && Mix)
            {
                for (var i = 0; i < strike.Reagents; i++)
                    AddReagent(S.Reagent);
            }
            if (strike.Flask && Mix)
                ThrowFlask();
            if (strike.Layers > 0 && Solvent)
                RemoveLayers(strike.Layers);
            if (strike.Elixir && Elixirs)
                SendElixir();
            if (strike.Corrode > 0)
                Corrode(strike.Corrode, strike.CorrodeSeconds > 0 ? strike.CorrodeSeconds : 8);
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (S.SpawnPending)
                OnNewEnemy();
            if (S.MixTicks > 0 && --S.MixTicks == 0)
                S.MixDamageTaken = 0;
            if (S.MixCritTicks > 0 && --S.MixCritTicks == 0)
                S.MixCrit = 0;
            if (S.MixGoldTicks > 0 && --S.MixGoldTicks == 0)
                S.MixGold = 0;
            if (Mix && InParty && S.CallTicks-- <= 0)
                CallReagents();
            _state.TraitPartyCritChance = S.MixCrit;
            _state.TraitPartyGold = S.MixGold;
            ShareMix();
            ShareSolvent();
            ShareFlask();
        }

        public void ReceiveReagent(int slot)
        {
            if (Mix && slot >= 0)
                AddReagent(slot % AlchemistRules.Colors);
        }

        public void ReceiveAcidHits(int hits)
        {
            if (Solvent && hits > 0)
                Dissolve(hits);
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            S.MixDamageTaken = 0;
            S.MixTicks = 0;
            S.LayerProgress = 0;
            S.LayersLeft = Solvent ? _rules.Layers - System.Math.Min(_rules.Layers, S.CarryLayers) : -1;
            S.CarryLayers = 0;
            S.MixResendTicks = 0;
            S.SolventResendTicks = 0;
        }

        private void MixStrike(bool crit)
        {
            S.Reagent = (S.Reagent + 1) % AlchemistRules.Colors;
            if (crit)
                AddReagent(S.Reagent);
            if (++S.ReagentHits < ReagentHits)
                return;
            S.ReagentHits = 0;
            AddReagent(S.Reagent);
        }

        private void AddReagent(int color)
        {
            if (S.InFlask < AlchemistState.FlaskSize)
                S.Flask[S.InFlask++] = color;
            if (S.InFlask >= AlchemistState.FlaskSize)
                Brew(1);
        }

        private void ThrowFlask()
        {
            while (S.InFlask < AlchemistState.FlaskSize)
                S.Flask[S.InFlask++] = S.Reagent;
            Brew(_rules.FlaskBoost);
        }

        private void Brew(double boost)
        {
            int red = 0, blue = 0, gold = 0;
            for (var i = 0; i < S.InFlask; i++)
            {
                if (S.Flask[i] == 0)
                    red++;
                else if (S.Flask[i] == 1)
                    blue++;
                else
                    gold++;
            }
            S.InFlask = 0;
            S.Mixes++;
            var recipe = AlchemistRules.RecipeOf(red, blue, gold);
            S.LastRecipe = recipe;
            var power = MixPower * boost;
            var pure = red == 3 || blue == 3 || gold == 3;
            if (pure && Flag(AlchemistStats.MixPure))
                power *= _rules.PureBoost;
            var philosopher = red == 1 && blue == 1 && gold == 1;
            if (Flag(AlchemistStats.MixStone))
                power *= philosopher ? _rules.PhilosopherBoost : 0.7;
            var times = Flag(AlchemistStats.MixLucky) && S.Mixes % _rules.LuckyEvery == 0 ? 2 : 1;
            for (var time = 0; time < times; time++)
            {
                Apply(_rules.Mix(recipe), power);
                if (Flag(AlchemistStats.MixChain))
                    TraitHit(_rules.ChainBlast * power);
                if (Flag(AlchemistStats.MixQueue))
                    _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, _rules.QueueBlast * power * _state.FocusMultiplier, StatTags.Keyboard));
            }
        }

        private void Apply(System.Collections.Generic.IReadOnlyList<AlchemistMixEffect> effects, double power)
        {
            var longer = Flag(AlchemistStats.MixLong) ? 2 : 1;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                var seconds = effect.Seconds * longer;
                switch (effect.Kind)
                {
                    case AlchemistMixKind.Blast:
                        TraitHit(effect.Value * power);
                        break;
                    case AlchemistMixKind.Corrode:
                        Corrode(effect.Value * power + Positive(AlchemistStats.MixParty), seconds);
                        break;
                    case AlchemistMixKind.Resource:
                        _state.PendingResource += effect.Value * power;
                        _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, effect.Value * power);
                        break;
                    case AlchemistMixKind.Stop:
                        StopTimer(seconds);
                        break;
                    case AlchemistMixKind.Crit:
                        S.MixCrit = System.Math.Max(S.MixCrit, effect.Value * power);
                        S.MixCritTicks = System.Math.Max(S.MixCritTicks, Ticks(seconds));
                        break;
                    case AlchemistMixKind.Gold:
                        S.MixGold = System.Math.Max(S.MixGold, effect.Value * power);
                        S.MixGoldTicks = System.Math.Max(S.MixGoldTicks, Ticks(seconds));
                        break;
                    case AlchemistMixKind.Cut:
                        CutCooldowns(effect.Value);
                        break;
                }
            }
        }

        private void Corrode(double share, double seconds)
        {
            S.MixDamageTaken = System.Math.Max(MixShare, share);
            S.MixTicks = System.Math.Max(S.MixTicks, Ticks(seconds));
            S.MixResendTicks = 0;
        }

        private void StopTimer(double seconds)
        {
            if (!_state.FightingBoss)
                return;
            if (_state.SharedEnemy)
                _state.TraitEnemyEffects.Add(StopEffect, 0, seconds, 1);
            else
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, Ticks(seconds));
        }

        private void CutCooldowns(double seconds)
        {
            var cut = Ticks(seconds);
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                var slot = _state.Skills[i];
                if (slot.CooldownTicks > 1)
                    slot.CooldownTicks = System.Math.Max(1, slot.CooldownTicks - cut);
            }
            if (InParty)
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.GearTick, To = TraitRelay.AllSlots, Seconds = seconds });
        }

        private void Dissolve(double hits)
        {
            if (!_state.EnemyAlive || S.LayersLeft <= 0)
                return;
            S.LayerProgress += hits;
            var need = LayerHits;
            while (S.LayerProgress >= need && S.LayersLeft > 0)
            {
                S.LayerProgress -= need;
                RemoveLayers(1);
            }
        }

        private void RemoveLayers(int count)
        {
            if (S.LayersLeft < 0)
                S.LayersLeft = _rules.Layers;
            var removed = System.Math.Min(count, S.LayersLeft);
            if (removed <= 0)
                return;
            S.LayersLeft -= removed;
            S.LayersDissolved += removed;
            S.SolventResendTicks = 0;
            if (Flag(AlchemistStats.LayerBlast))
                TraitHit(_rules.LayerBlast * removed);
            if (Flag(AlchemistStats.SolventCarry))
                S.CarryLayers = 1;
            GiveAcid();
        }

        private void GiveAcid()
        {
            var seconds = System.Math.Max(1, _rules.AcidSeconds + _hero.Value(AlchemistStats.AcidSeconds, 0));
            if (!InParty)
            {
                Boons.AcidOwner = -1;
                Boons.AcidTicks = System.Math.Max(Boons.AcidTicks, Ticks(seconds * (Flag(AlchemistStats.AcidAll) ? 1.5 : 1)));
                return;
            }
            _state.TraitRelays.Add(new TraitRelay
            {
                Kind = TraitRelayKind.Acid,
                To = Flag(AlchemistStats.AcidAll) ? TraitRelay.AllSlots : LanternbearerMechanics.AnyAlly,
                Seconds = seconds
            });
        }

        private void SendElixir()
        {
            var color = S.ElixirColor;
            S.ElixirColor = (S.ElixirColor + 1) % AlchemistRules.Colors;
            S.ElixirsSent++;
            var power = ElixirPower * (Flag(AlchemistStats.ElixirAll) ? 0.6 : 1) * (1 + Positive(AlchemistStats.ElixirParty));
            var seconds = ElixirSeconds;
            var philosopher = Flag(AlchemistStats.ElixirStone) ? _rules.StoneSeconds : _rules.PhilosopherSeconds;
            var every = Flag(AlchemistStats.ElixirDouble) ? 2 : _rules.PhilosopherEvery;
            if (!InParty)
            {
                var times = Flag(AlchemistStats.ElixirTwo) ? 2 : 1;
                Boons.Drink(color, power, Ticks(seconds * times), _rules.RedEvery, _rules.GoldBonus, every, Ticks(philosopher));
                return;
            }
            var relay = new TraitRelay
            {
                Kind = TraitRelayKind.Elixir,
                To = Flag(AlchemistStats.ElixirAll) ? TraitRelay.AllSlots : LanternbearerMechanics.AnyAlly,
                Count = (byte)(color | (System.Math.Min(63, (int)System.Math.Round(philosopher)) << 2)),
                Every = (byte)_rules.RedEvery,
                Max = (byte)every,
                Value = power,
                Seconds = seconds,
                Extra = _rules.GoldBonus
            };
            _state.TraitRelays.Add(relay);
            if (Flag(AlchemistStats.ElixirTwo) && !Flag(AlchemistStats.ElixirAll))
                _state.TraitRelays.Add(relay);
        }

        public static void DrinkRelay(AllyBoons boons, in TraitRelay relay)
        {
            boons.Drink(relay.Count & 3, relay.Value, Ticks(relay.Seconds), relay.Every, relay.Extra, System.Math.Max(1, (int)relay.Max), Ticks(relay.Count >> 2));
        }

        private void CallReagents()
        {
            S.CallTicks = Ticks(_rules.ReagentCallSeconds / 3);
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Reagents, To = TraitRelay.AllSlots, Seconds = _rules.ReagentCallSeconds });
        }

        private void ShareMix()
        {
            if (!InParty || !(MixShare > 0) || S.MixResendTicks-- > 0)
                return;
            S.MixResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(MixEffect, MixShare, _rules.MixEffectSeconds);
        }

        private void ShareSolvent()
        {
            var share = SolventDamageTaken;
            if (!InParty || !(share > 0) || S.SolventResendTicks-- > 0)
                return;
            S.SolventResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(SolventEffect, share, _rules.SolventEffectSeconds);
        }

        private void ShareFlask()
        {
            if (!InParty || !Mix)
                return;
            var flask = FlaskCode();
            if (flask == S.SentFlask)
                return;
            S.SentFlask = flask;
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Flask, To = TraitRelay.AllSlots, Count = (byte)flask });
        }

        private int FlaskCode()
        {
            var code = 0;
            for (var i = 0; i < S.InFlask; i++)
                code |= (S.Flask[i] + 1) << (2 * i);
            return code;
        }

        private void TickBoons()
        {
            if (Boons.ReagentTicks > 0)
                Boons.ReagentTicks--;
            if (Boons.ReagentCooldownTicks > 0)
                Boons.ReagentCooldownTicks--;
            if (Boons.AcidTicks > 0)
                Boons.AcidTicks--;
            if (Boons.RedTicks > 0)
                Boons.RedTicks--;
            if (Boons.BlueTicks > 0)
                Boons.BlueTicks--;
            if (Boons.GoldTicks > 0)
                Boons.GoldTicks--;
            if (Boons.ReagentOwed && Boons.ReagentOwner >= 0)
            {
                Boons.ReagentOwed = false;
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Reagent, To = (byte)Boons.ReagentOwner, Count = 1 });
            }
            if (Boons.AcidHitsOwed <= 0 || Boons.AcidOwner < 0)
                return;
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.AcidHit, To = (byte)Boons.AcidOwner, Count = (byte)System.Math.Min(byte.MaxValue, Boons.AcidHitsOwed) });
            Boons.AcidHitsOwed = 0;
        }

        private void StrikeBoons(bool crit)
        {
            if (crit && Boons.ReagentTicks > 0 && Boons.ReagentOwner >= 0 && Boons.ReagentCooldownTicks <= 0)
            {
                Boons.ReagentOwed = true;
                Boons.ReagentCooldownTicks = Ticks(_rules.ReagentCooldownSeconds);
            }
            if (Boons.AcidTicks > 0 && Boons.AcidOwner >= 0)
                Boons.AcidHitsOwed++;
            if (Boons.RedTicks <= 0 || ++Boons.RedStrikes < Boons.RedEvery)
                return;
            Boons.RedStrikes = 0;
            TraitHit(Boons.ElixirPower);
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
