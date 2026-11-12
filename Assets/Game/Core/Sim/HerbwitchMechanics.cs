using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class HerbwitchMechanics : IKillRewardListener
    {
        public static readonly int GardenEffect = GemDef.NetIdOf("trait_herb_garden");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private HerbwitchRules _rules;

        public HerbwitchMechanics(HerbwitchRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(HerbwitchRules rules) => _rules = rules;

        private HerbwitchState S => _state.Herbwitch;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Herbwitch);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Bloom => On && BloomPower > 0;
        public bool Mycelium => On && MyceliumPower > 0;
        public bool Sap => On && SapPower > 0;
        public bool Traited => Bloom || Mycelium || Sap;

        public double BloomPower => Positive(HerbwitchStats.BloomPower);
        public double MyceliumPower => Positive(HerbwitchStats.MyceliumPower);
        public double SapPower => Positive(HerbwitchStats.SapPower);

        public int MaxVines => Flag(HerbwitchStats.BloomSingle) ? 1
            : System.Math.Max(1, System.Math.Min(System.Math.Min(_rules.VinesCap, HerbwitchState.Capacity), _rules.Vines + (int)System.Math.Round(_hero.Value(HerbwitchStats.BloomVines, 0))));

        public int SeedHits => System.Math.Max(_rules.SeedHitsMin, (int)System.Math.Round(_rules.SeedHits + _hero.Value(HerbwitchStats.BloomHits, 0)));

        public double GrowSeconds => System.Math.Max(_rules.GrowSecondsMin, _rules.GrowSeconds + _hero.Value(HerbwitchStats.BloomGrow, 0));

        public int TopStage => Flag(HerbwitchStats.BloomThorn) ? 1 : 2;

        public double VenomMax => System.Math.Max(1, _rules.VenomMax + _hero.Value(HerbwitchStats.MyceliumMax, 0));

        public int DrinkHits => System.Math.Max(_rules.DrinkHitsMin, (int)System.Math.Round(_rules.DrinkHits + _hero.Value(HerbwitchStats.SapHits, 0)));

        public double VineDamage(int stage)
        {
            var growth = stage == 1 && Flag(HerbwitchStats.BloomThorn) ? _rules.ThornMultiplier : System.Math.Pow(_rules.StageMultiplier, stage);
            return BloomPower * growth * (Flag(HerbwitchStats.BloomSingle) ? _rules.SingleMultiplier : 1);
        }

        public void AfterStrike()
        {
            BoonStrike();
            if (!On)
                return;
            if (Bloom && ++S.SeedHits >= SeedHits)
            {
                S.SeedHits = 0;
                Plant(1);
            }
            if (Mycelium)
                S.Venom = System.Math.Min(VenomMax, S.Venom + (Flag(HerbwitchStats.MyceliumDouble) ? 2 : 1));
            if (!Sap)
                return;
            S.Brew = System.Math.Min(_rules.BrewMax, S.Brew + _rules.BrewPerHit);
            if (++S.DrinkHits < DrinkHits)
                return;
            S.DrinkHits = 0;
            Drink(1);
        }

        public void OnSkillCast(GemDef skill, SkillDef? variant = null)
        {
            if (!On || !((variant ?? skill.Skill)?.CastStrike is StrikeEffect strike))
                return;
            if (strike.Seeds > 0 && Bloom)
                Plant(strike.Seeds);
            if (strike.Bloom && Bloom)
                BloomAll();
            if (strike.Venom > 0 && Mycelium)
                S.Venom = System.Math.Min(VenomMax, S.Venom + strike.Venom);
            if (strike.VenomBurst > 0 && S.Venom > 0)
                TraitHit(S.Venom * MyceliumPower * strike.VenomBurst, StatTags.DoT);
            if (strike.Drink && Sap && S.Brew > 0)
                Drink(1);
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (S.Spawned)
                OnNewEnemy();
            if (Bloom)
                TickGarden();
            if (Mycelium)
                TickVenom();
            if (Sap)
                TickSap();
        }

        public void OnKillRewarded()
        {
            if (Mycelium && !Flag(HerbwitchStats.MyceliumNoCarry))
                S.VenomCarryOver = S.Venom * (Flag(HerbwitchStats.MyceliumDeep) ? _rules.VenomDeepCarry : _rules.VenomCarry);
            if (Bloom && Flag(HerbwitchStats.BloomReplant))
            {
                S.FlowerCarry = 0;
                for (var i = 0; i < S.Vines; i++)
                {
                    if (S.VineStage[i] >= TopStage)
                        S.FlowerCarry++;
                }
            }
        }

        private void OnNewEnemy()
        {
            S.Spawned = false;
            var replant = S.FlowerCarry;
            S.FlowerCarry = 0;
            S.Vines = 0;
            S.VineCarry = 0;
            if (Bloom)
            {
                for (var i = 0; i < replant && S.Vines < MaxVines; i++)
                {
                    S.VineStage[S.Vines] = TopStage;
                    S.VineGrowTicks[S.Vines] = 0;
                    S.Vines++;
                }
            }
            S.Venom = S.VenomCarryOver;
            S.VenomCarryOver = 0;
            if (Sap && !Flag(HerbwitchStats.SapLeave))
                S.Brew = 0;
            else if (Sap)
                S.Brew *= _rules.LeaveShare;
        }

        private void Plant(int count)
        {
            for (var i = 0; i < count; i++)
            {
                S.Seeds++;
                if (S.Vines >= MaxVines)
                {
                    GrowOldest();
                    continue;
                }
                S.VineStage[S.Vines] = Flag(HerbwitchStats.BloomStart) ? 1 : 0;
                S.VineGrowTicks[S.Vines] = Ticks(GrowSeconds);
                S.Vines++;
            }
        }

        private void GrowOldest()
        {
            for (var i = 0; i < S.Vines; i++)
            {
                if (S.VineStage[i] >= TopStage)
                    continue;
                Grow(i);
                return;
            }
        }

        private void BloomAll()
        {
            if (S.Vines == 0)
                Plant(MaxVines);
            for (var i = 0; i < S.Vines; i++)
            {
                while (S.VineStage[i] < TopStage)
                    Grow(i);
            }
        }

        private void Grow(int vine)
        {
            S.VineStage[vine]++;
            S.VineGrowTicks[vine] = Ticks(GrowSeconds);
            if (S.VineStage[vine] < TopStage || TopStage < 2)
                return;
            S.Flowers++;
            DropPetal();
        }

        private void DropPetal()
        {
            LandPetal(Boons, _rules.PetalHits, BloomPower * (1 + Positive(HerbwitchStats.BloomParty)), _rules.PetalSeconds);
        }

        private static void LandPetal(AllyBoons boons, int every, double power, double seconds, int maxSeeds = 3)
        {
            boons.PetalEvery = System.Math.Max(1, every);
            boons.PetalPower = power;
            boons.PetalTicks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            boons.PetalMax = System.Math.Max(1, maxSeeds);
        }

        private void TickGarden()
        {
            for (var i = 0; i < S.Vines; i++)
            {
                if (S.VineStage[i] >= TopStage || --S.VineGrowTicks[i] > 0)
                    continue;
                Grow(i);
            }
            if (S.Vines > 0 && _state.EnemyAlive)
            {
                S.VineCarry += GameLoop.TickSeconds;
                if (S.VineCarry >= 1 - 1e-9)
                {
                    S.VineCarry -= 1;
                    var total = 0.0;
                    for (var i = 0; i < S.Vines; i++)
                        total += VineDamage(S.VineStage[i]);
                    TraitHit(total);
                    if (Flag(HerbwitchStats.BloomQueue))
                        Queue(total * _rules.QueueShare);
                }
            }
            if (!InParty || S.Vines == 0 || S.GardenResendTicks-- > 0)
                return;
            S.GardenResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(GardenEffect, 0, _rules.GardenEffectSeconds);
        }

        private void TickVenom()
        {
            if (S.Venom > 0 && _state.EnemyAlive)
            {
                S.VenomTickCarry += GameLoop.TickSeconds * (Flag(HerbwitchStats.MyceliumFast) ? _rules.FastMultiplier : 1);
                while (S.VenomTickCarry >= 1 - 1e-9)
                {
                    S.VenomTickCarry -= 1;
                    var power = S.Venom * MyceliumPower;
                    TraitHit(power, StatTags.DoT);
                    if (Flag(HerbwitchStats.MyceliumQueue))
                        Queue(power * _rules.VenomQueueShare);
                }
            }
        }

        private void TickSap()
        {
            if (S.PartyPoisonTicks > 0)
            {
                S.PartyPoisonTicks--;
                S.PoisonCarry += GameLoop.TickSeconds;
                if (S.PoisonCarry >= 1 - 1e-9)
                {
                    S.PoisonCarry -= 1;
                    TraitHit(SapPower * 10, StatTags.DoT);
                }
            }
        }

        private void Drink(double share)
        {
            if (S.Brew <= 0)
                return;
            var drunk = S.Brew * share;
            var leave = Flag(HerbwitchStats.SapLeave) ? _rules.LeaveShare : 0;
            S.Brew -= drunk;
            S.Brew += drunk * leave;
            S.Drinks++;
            var greedy = Flag(HerbwitchStats.SapGreedy) ? 2 : 1;
            TraitHit(drunk * SapPower * greedy, StatTags.DoT);
            _state.PendingResource += drunk * _rules.SapPerStack;
            if (Flag(HerbwitchStats.SapHeal))
                _state.PendingResource += drunk * _rules.SapPerStack;
            if (Flag(HerbwitchStats.SapPoison))
                S.PartyPoisonTicks = Ticks(_rules.PartyPoisonSeconds);
        }

        private void BoonStrike()
        {
            if (Boons.PetalTicks > 0 && ++Boons.PetalHits >= Boons.PetalEvery)
            {
                Boons.PetalHits = 0;
                Boons.PetalSeeds = System.Math.Min(Boons.PetalMax, Boons.PetalSeeds + 1);
            }
        }

        private void TickBoons()
        {
            if (Boons.PetalTicks > 0)
            {
                if (--Boons.PetalTicks == 0)
                    Boons.PetalSeeds = 0;
                if (Boons.PetalSeeds > 0 && _state.EnemyAlive)
                {
                    Boons.PetalCarry += GameLoop.TickSeconds;
                    if (Boons.PetalCarry >= 1 - 1e-9)
                    {
                        Boons.PetalCarry -= 1;
                        Boons.PetalStings++;
                        TraitHit(Boons.PetalSeeds * Boons.PetalPower);
                    }
                }
            }
        }

        private void Queue(double multiplier) =>
            _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Strike));

        private void TraitHit(double multiplier, StatTags extra = StatTags.None)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Strike | extra);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
