using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class CartomancerMechanics : IKillRewardListener
    {
        public static readonly int ForecastEffect = GemDef.NetIdOf("trait_forecast");
        public static readonly int ReversedEffect = GemDef.NetIdOf("trait_reversed_cards");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private CartomancerRules _rules;

        public CartomancerMechanics(CartomancerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(CartomancerRules rules) => _rules = rules;

        private CartomancerState S => _state.Cartomancer;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Cartomancer);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Spread => On && SpreadPower > 0;
        public bool Forecast => On && ForecastPower > 0;
        public bool Reversed => On && ReversedPower > 0;
        public bool Traited => Spread || Forecast || Reversed;

        public double SpreadPower => Positive(CartomancerStats.SpreadPower);
        public double ForecastPower => Positive(CartomancerStats.ForecastPower);
        public double ReversedPower => Positive(CartomancerStats.ReversedPower);

        public int DrawHits => System.Math.Max(_rules.DrawHitsMin, (int)System.Math.Round(_rules.DrawHits + _hero.Value(CartomancerStats.SpreadHits, 0)));

        public int ArcanaCards => Flag(CartomancerStats.SpreadPair) ? 2 : _rules.ArcanaCards;

        public double ArcanaSeconds => (Flag(CartomancerStats.SpreadPair) ? _rules.ArcanaShortSeconds : _rules.ArcanaSeconds)
            + (Flag(CartomancerStats.SpreadLong) ? _rules.LongSeconds : 0) + _hero.Value(CartomancerStats.SpreadSeconds, 0);

        public int LayHits => System.Math.Max(_rules.LayHitsMin, (int)System.Math.Round(_rules.LayHits + _hero.Value(CartomancerStats.ReversedHits, 0)));

        public int LaidMax => System.Math.Max(1, System.Math.Min(_rules.LaidCap, _rules.LaidMax + (int)System.Math.Round(_hero.Value(CartomancerStats.ReversedMax, 0))));

        public int RevealHits => System.Math.Max(1, (int)System.Math.Round(_rules.RevealHits + _hero.Value(CartomancerStats.ReversedRevealHits, 0)));

        public bool ArcanaActive(int suit) => S.ArcanaTicks[suit] > 0;

        public double SuitPower(int suit)
        {
            if (!Flag(CartomancerStats.SpreadSwords))
                return SpreadPower;
            return suit == CartomancerState.Swords ? SpreadPower * _rules.SwordsBoost : SpreadPower * _rules.OtherPenalty;
        }

        public double SwordsShare => Reversed ? S.Laid[CartomancerState.Swords] * ReversedPower * RevealFactor(CartomancerState.Swords) : 0;

        public double WandsSlow => Reversed
            ? System.Math.Min(_rules.WandsSlowMax, S.Laid[CartomancerState.Wands] * ReversedPower * _rules.WandsSlow * RevealFactor(CartomancerState.Wands))
            : 0;

        public double DamageTaken()
        {
            var multiplier = 1.0;
            if (Boons.ForecastPenalty > 0)
                multiplier *= 1 - Boons.ForecastPenalty;
            if (!On)
                return multiplier;
            if (S.Penalty)
                multiplier *= 1 - Penalty;
            if (Spread && ArcanaActive(CartomancerState.Swords))
                multiplier *= 1 + SuitPower(CartomancerState.Swords);
            if (Spread && Flag(CartomancerStats.SpreadHand))
                multiplier *= 1 + S.HandCount * _rules.HandBonus * SpreadPower / 0.5;
            if (Forecast && Flag(CartomancerStats.ForecastRush) && S.TermTicks > 0 && S.TermTicks <= Ticks(_rules.RushSeconds))
                multiplier *= 1 + _rules.Rush;
            if (Reversed && !_state.SharedEnemy)
                multiplier *= 1 + SwordsShare;
            return multiplier;
        }

        public void AddAllyHits(double hits)
        {
            if (Reversed && hits > 0 && S.LaidCount > 0)
                AddRevealHits(hits);
        }

        public void AfterStrike()
        {
            BoonStrike();
            if (!On)
                return;
            if (Spread && ++S.DrawHits >= DrawHits)
            {
                S.DrawHits = 0;
                Draw(1);
            }
            if (Reversed)
            {
                if (++S.LayHits >= LayHits)
                {
                    S.LayHits = 0;
                    Lay(1);
                }
                if (S.LaidCount > 0)
                    AddRevealHits(InParty ? 1 : _rules.RevealSoloShare);
            }
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.Cards > 0 && Spread)
                Draw(strike.Cards);
            if (strike.Cards > 0 && Reversed)
                Lay(strike.Cards);
            if (strike.Arcana && Spread)
                OpenArcana(_state.Rng.NextInt(0, CartomancerState.Suits));
            if (strike.Reveal && Reversed && S.LaidCount > 0)
                Reveal();
            if (strike.HandHit > 0 && S.HandCount + S.LaidCount > 0)
                TraitHit(strike.HandHit * (S.HandCount + S.LaidCount));
            if (strike.Forecast && Forecast && _state.FightingBoss)
                StartTerm();
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (S.Spawned)
                OnNewEnemy();
            if (Spread)
                TickSpread();
            if (Forecast)
                TickForecast();
            if (Reversed)
                TickReversed();
        }

        public void OnKillRewarded()
        {
            if (!Forecast || !_state.FightingBoss || S.TermTicks <= 0)
                return;
            S.TermTicks = 0;
            S.GoldAdded = 0;
            S.Wins++;
            S.Streak = System.Math.Min(_rules.StreakMax, S.Streak + 1);
        }

        public void ReceiveCard(int suit)
        {
            if (!Spread || suit < 0 || suit >= CartomancerState.Suits)
                return;
            S.AllyCards++;
            AddCard(suit);
        }

        public static int SuitOf(HeroClass heroClass) => heroClass switch
        {
            HeroClass.Runedancer or HeroClass.Duelist or HeroClass.Windshot or HeroClass.Shardcaller or HeroClass.Alchemist => CartomancerState.Swords,
            HeroClass.Minstrel or HeroClass.Lanternbearer or HeroClass.Soulbinder or HeroClass.Ghostcaptain or HeroClass.Golemancer => CartomancerState.Cups,
            HeroClass.Dowser or HeroClass.Dreamcatcher => CartomancerState.Coins,
            _ => CartomancerState.Wands
        };

        private void OnNewEnemy()
        {
            S.Spawned = false;
            if (!Reversed)
                return;
            var keep = Flag(CartomancerStats.ReversedKeep) ? _rules.KeepShare : 0;
            for (var suit = 0; suit < CartomancerState.Suits; suit++)
                S.Laid[suit] = (int)System.Math.Floor(S.Laid[suit] * keep);
            S.RevealTicks = 0;
            S.RevealSuit = -1;
            var coins = S.Laid[CartomancerState.Coins];
            if (coins > 0)
                _state.EnemyGoldBonus += coins * ReversedPower * _rules.CoinsPerCard;
        }

        private void Draw(int count)
        {
            for (var i = 0; i < count; i++)
            {
                S.Draws++;
                AddCard(_state.Rng.NextInt(0, CartomancerState.Suits));
            }
        }

        private void AddCard(int suit)
        {
            if (S.HandCount >= _rules.HandMax)
                DiscardOne();
            S.Hand[suit]++;
            if (Flag(CartomancerStats.SpreadHand) || S.Hand[suit] < ArcanaCards)
                return;
            S.Hand[suit] -= ArcanaCards;
            OpenArcana(suit);
        }

        private void DiscardOne()
        {
            var largest = 0;
            for (var suit = 1; suit < CartomancerState.Suits; suit++)
            {
                if (S.Hand[suit] > S.Hand[largest])
                    largest = suit;
            }
            if (S.Hand[largest] > 0)
                S.Hand[largest]--;
        }

        private void OpenArcana(int suit)
        {
            S.Arcana++;
            S.ArcanaTicks[suit] = Ticks(ArcanaSeconds);
            if (suit == CartomancerState.Coins)
                S.CoinsApplied = false;
        }

        private void TickSpread()
        {
            for (var suit = 0; suit < CartomancerState.Suits; suit++)
            {
                if (S.ArcanaTicks[suit] > 0)
                    S.ArcanaTicks[suit]--;
            }
            if (ArcanaActive(CartomancerState.Coins) && !S.CoinsApplied && _state.EnemyAlive)
            {
                S.CoinsApplied = true;
                _state.EnemyGoldBonus += SuitPower(CartomancerState.Coins) * _rules.CoinsGold;
            }
            if (++S.ArcanaSecondTicks >= GameLoop.TicksPerSecond)
            {
                S.ArcanaSecondTicks = 0;
                if (ArcanaActive(CartomancerState.Cups))
                    _state.PendingResource += _rules.CupsResource * SuitPower(CartomancerState.Cups);
                if (ArcanaActive(CartomancerState.Wands))
                    CutCooldowns(_rules.WandsCut * SuitPower(CartomancerState.Wands));
            }
            OfferDeal();
        }

        private void OfferDeal()
        {
            if (!InParty || S.DealOfferTicks-- > 0)
                return;
            S.DealOfferTicks = Ticks(_rules.DealOfferSeconds) - 1;
            _state.TraitRelays.Add(new TraitRelay
            {
                Kind = TraitRelayKind.Deal,
                To = TraitRelay.AllSlots,
                Every = (byte)System.Math.Min(byte.MaxValue, System.Math.Max(1, (int)System.Math.Round(_rules.DealHits / (1 + Positive(CartomancerStats.SpreadParty))))),
                Seconds = _rules.DealOfferSeconds * 2
            });
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
        }

        private double Penalty =>
            (_rules.Penalty + (Flag(CartomancerStats.ForecastExact) ? _rules.ExactPenalty : 0)) * (Flag(CartomancerStats.ForecastDouble) ? 2 : 1);

        private double Reward =>
            ForecastPower * (1 + (Flag(CartomancerStats.ForecastStreak) ? S.Streak * _rules.StreakStep : 0)) * (Flag(CartomancerStats.ForecastDouble) ? 2 : 1);

        private void TickForecast()
        {
            if (_state.FightingBoss && !S.BossSeen)
            {
                S.BossSeen = true;
                S.Penalty = false;
                StartTerm();
            }
            if (S.TermTicks <= 0)
                return;
            if (!_state.FightingBoss || !_state.EnemyAlive)
            {
                S.TermTicks = 0;
                return;
            }
            if (Flag(CartomancerStats.ForecastExact) && !S.ExactPaid && S.TermTicks <= Ticks(_rules.ExactSeconds))
            {
                S.ExactPaid = true;
                _state.EnemyGoldBonus += Reward * _rules.ExactGold;
                S.GoldAdded += Reward * _rules.ExactGold;
            }
            if (--S.TermTicks > 0)
                return;
            Lose();
        }

        private void StartTerm()
        {
            var share = _rules.TermShare * (Flag(CartomancerStats.ForecastLong) ? _rules.LongTerm : 1) * (Flag(CartomancerStats.ForecastShort) ? _rules.ShortTerm : 1);
            var seconds = System.Math.Max(_rules.TermMinSeconds, _state.BossSecondsLeft * share);
            if (S.GoldAdded > 0)
                _state.EnemyGoldBonus = System.Math.Max(0, _state.EnemyGoldBonus - S.GoldAdded);
            S.TermTicks = Ticks(seconds);
            S.ExactPaid = false;
            S.GoldAdded = Reward;
            _state.EnemyGoldBonus += S.GoldAdded;
            if (!InParty)
                return;
            _state.TraitEnemyEffects.Add(ForecastEffect, 0, seconds);
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Forecast, To = TraitRelay.AllSlots, Value = S.GoldAdded * (1 + Positive(CartomancerStats.ForecastParty)), Seconds = seconds });
        }

        private void Lose()
        {
            S.Losses++;
            S.Streak = 0;
            _state.EnemyGoldBonus = System.Math.Max(0, _state.EnemyGoldBonus - S.GoldAdded);
            S.GoldAdded = 0;
            if (Flag(CartomancerStats.ForecastSafe))
                return;
            S.Penalty = true;
            if (InParty)
                _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.ForecastLost, To = TraitRelay.AllSlots, Value = Penalty });
        }

        private void Lay(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (S.LaidCount >= LaidMax)
                    return;
                var suit = _state.Rng.NextInt(0, CartomancerState.Suits);
                var cards = Flag(CartomancerStats.ReversedFifth) && (S.Lays + 1) % 5 == 0 ? 2 : 1;
                S.Lays++;
                S.Laid[suit] += cards;
                if (suit == CartomancerState.Coins && _state.EnemyAlive)
                    _state.EnemyGoldBonus += cards * ReversedPower * _rules.CoinsPerCard;
            }
            S.EffectResendTicks = 0;
        }

        private void AddRevealHits(double hits)
        {
            S.RevealHits += hits;
            var need = RevealHits;
            while (S.RevealHits >= need)
            {
                S.RevealHits -= need;
                Reveal();
            }
        }

        private void Reveal()
        {
            var top = 0;
            for (var suit = 1; suit < CartomancerState.Suits; suit++)
            {
                if (S.Laid[suit] > S.Laid[top])
                    top = suit;
            }
            if (S.Laid[top] <= 0)
                return;
            S.Reveals++;
            S.RevealSuit = top;
            S.RevealTicks = Ticks(_rules.RevealSeconds);
            S.EffectResendTicks = 0;
        }

        private double RevealFactor(int suit) =>
            S.RevealTicks > 0 && S.RevealSuit == suit ? (Flag(CartomancerStats.ReversedStrong) ? _rules.RevealStrong : _rules.RevealMultiplier) : 1;

        private void TickReversed()
        {
            if (S.RevealTicks > 0 && --S.RevealTicks == 0)
                S.RevealSuit = -1;
            var cups = S.Laid[CartomancerState.Cups];
            if (cups > 0 && _state.EnemyAlive)
            {
                S.CupsCarry += GameLoop.TickSeconds;
                if (S.CupsCarry >= 1 - 1e-9)
                {
                    S.CupsCarry -= 1;
                    TraitHit(cups * ReversedPower * _rules.CupsHit * RevealFactor(CartomancerState.Cups), StatTags.DoT);
                }
            }
            if (!_state.SharedEnemy)
            {
                _state.TraitTimerSlow = _state.FightingBoss ? WandsSlow : 0;
                return;
            }
            _state.TraitTimerSlow = 0;
            if (!InParty || S.LaidCount <= 0 || S.EffectResendTicks-- > 0)
                return;
            S.EffectResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(ReversedEffect, SwordsShare * (1 + Positive(CartomancerStats.ReversedParty)), _rules.EffectSeconds, WandsSlow);
        }

        private void BoonStrike()
        {
            if (Boons.DealTicks <= 0 || Boons.DealOwner < 0)
                return;
            if (++Boons.DealHits < System.Math.Max(1, Boons.DealEvery))
                return;
            Boons.DealHits = 0;
            Boons.CardsDealt++;
            Boons.LastSuit = SuitOf(_state.Class);
            _state.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Card, To = (byte)Boons.DealOwner, Count = (byte)Boons.LastSuit });
        }

        private void TickBoons()
        {
            if (Boons.DealTicks > 0)
                Boons.DealTicks--;
            if (Boons.ForecastTicks <= 0)
                return;
            Boons.ForecastTicks--;
            if (!Boons.ForecastApplied && _state.FightingBoss && _state.EnemyAlive)
            {
                Boons.ForecastApplied = true;
                _state.EnemyGoldBonus += Boons.ForecastGold;
            }
        }

        public static void LandForecast(AllyBoons boons, double gold, double seconds)
        {
            boons.ForecastGold = gold;
            boons.ForecastTicks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            boons.ForecastApplied = false;
            boons.ForecastPenalty = 0;
        }

        public void LoseForecast(double penalty)
        {
            if (Boons.ForecastApplied && Boons.ForecastTicks > 0)
                _state.EnemyGoldBonus = System.Math.Max(0, _state.EnemyGoldBonus - Boons.ForecastGold);
            Boons.ForecastTicks = 0;
            Boons.ForecastPenalty = penalty;
        }

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
