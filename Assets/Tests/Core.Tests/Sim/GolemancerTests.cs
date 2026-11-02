using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GolemancerTests
    {
        private static GolemancerRules Rules => TestWorld.Data.Combat.Classes.Golemancer;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Golemancer, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Golemancer, EnemyType.Boss, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop, enemy);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop, EnemyType type)
        {
            loop.State.SpawnEnemy(null, type, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = type == EnemyType.Normal ? 0 : 100_000;
        }

        private static void Party(GameLoop loop, int members)
        {
            loop.State.SharedEnemy = true;
            for (var slot = 1; slot < members; slot++)
                loop.PartyBuffs.Ally(slot).Present = true;
            loop.State.PartyMembers = members;
        }

        private static List<double> Damage(GameLoop loop)
        {
            var damage = new List<double>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source != DamageSource.Mouse)
                    damage.Add(loop.Events[i].Amount.ToDouble());
            }
            return damage;
        }

        private static void Hits(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Golemancer.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static bool HasRelay(GameLoop loop, TraitRelayKind kind, out TraitRelay found)
        {
            for (var i = 0; i < loop.State.TraitRelays.Count; i++)
            {
                if (loop.State.TraitRelays[i].Kind != kind)
                    continue;
                found = loop.State.TraitRelays[i];
                return true;
            }
            found = default;
            return false;
        }


        [Test]
        public void AHundredHitsWriteASealAndAGolemStrikesEverySecondForTwentySeconds()
        {
            Assert.That(Rules.SealHits, Is.EqualTo(100));
            var (loop, input) = Hero(Flat(GolemancerStats.SealPower, 3));
            Hits(loop, input, 99);
            Assert.That(loop.Combat.Golemancer.GolemStanding, Is.False);
            Hits(loop, input, 1);
            Assert.That(loop.Combat.Golemancer.GolemStanding, Is.True);
            Assert.That(loop.State.Golemancer.Seals, Is.EqualTo(1));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            Wait(loop, Rules.GolemSeconds);
            Assert.That(loop.Combat.Golemancer.GolemStanding, Is.False);
        }

        [Test]
        public void WhileTheGolemStandsTheNextSealIsNotWritten()
        {
            var (loop, input) = Hero(Flat(GolemancerStats.SealPower, 3));
            Cast(loop, "golemancer_path_1");
            Hits(loop, input, 50);
            Assert.That(loop.State.Golemancer.SealHits, Is.Zero);
        }

        [Test]
        public void InAPartyAlliesWriteCornersAndEachCornerStrengthensTheGolem()
        {
            var (loop, _) = Hero(Flat(GolemancerStats.SealPower, 3));
            Party(loop, 3);
            loop.Tick();
            Assert.That(HasRelay(loop, TraitRelayKind.Corner, out var corner), Is.True);
            Assert.That(corner.To, Is.EqualTo(TraitRelay.AllSlots));
            Assert.That(corner.Every, Is.EqualTo(Rules.CornerHits));
            loop.Combat.Golemancer.ReceiveCorner(1);
            loop.Combat.Golemancer.ReceiveCorner(2);
            loop.Combat.Golemancer.ReceiveCorner(2);
            Cast(loop, "golemancer_path_1");
            Assert.That(loop.State.Golemancer.GolemPower, Is.EqualTo(3 * (1 + 2 * Rules.CornerBonus)).Within(1e-9));
            Assert.That(loop.State.Golemancer.Corners, Is.Zero);
        }

        [Test]
        public void AnAllyWritesOneCornerPerSeal()
        {
            var (ally, input) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            GolemancerMechanics.OfferCorner(ally.State.Boons, 0, 25, 1, 10);
            ally.State.TraitRelays.Clear();
            Hits(ally, input, 50);
            var corners = 0;
            for (var i = 0; i < ally.State.TraitRelays.Count; i++)
            {
                if (ally.State.TraitRelays[i].Kind == TraitRelayKind.CornerDone)
                    corners++;
            }
            Assert.That(corners, Is.EqualTo(1));
            GolemancerMechanics.OfferCorner(ally.State.Boons, 0, 25, 2, 10);
            Hits(ally, input, 25);
            Assert.That(ally.State.Boons.CornersWritten, Is.EqualTo(2));
        }

        [Test]
        public void TwoGolemsTitanAndRockfall()
        {
            var (two, _) = Hero(Flat(GolemancerStats.SealPower, 3), Flat(GolemancerStats.SealTwo, 1));
            Cast(two, "golemancer_path_1");
            Assert.That(two.State.Golemancer.SecondGolemTicks, Is.GreaterThan(0));
            var (titan, _) = Hero(Flat(GolemancerStats.SealPower, 3), Flat(GolemancerStats.SealTitan, 1));
            Cast(titan, "golemancer_path_1");
            Assert.That(titan.State.Golemancer.GolemPower, Is.EqualTo(6).Within(1e-9));
            Assert.That(titan.State.Golemancer.GolemTicks, Is.EqualTo((int)(Rules.TitanSeconds * GameLoop.TicksPerSecond)));
            var (crumble, _) = Hero(Flat(GolemancerStats.SealPower, 3), Flat(GolemancerStats.SealCrumble, 1));
            Cast(crumble, "golemancer_path_1");
            Wait(crumble, Rules.GolemSeconds + 0.2);
            Assert.That(crumble.State.QueueDamage[0].Sign, Is.GreaterThan(0));
        }

        [Test]
        public void TheGuardianGolemHoldsTheBossTimer()
        {
            var (loop, _) = Boss(Flat(GolemancerStats.SealPower, 3), Flat(GolemancerStats.SealGuard, 1));
            Cast(loop, "golemancer_path_1");
            Assert.That(loop.State.BossTimerStopTicks, Is.GreaterThan(0));
        }

        [Test]
        public void TheEternalGolemStrikesForeverAndRunesStrengthenIt()
        {
            var (loop, input) = Hero(Flat(GolemancerStats.EternalPower, 0.75));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            Hits(loop, input, Rules.RuneHits);
            Assert.That(loop.State.Golemancer.Runes, Is.EqualTo(1));
            Assert.That(loop.Combat.Golemancer.EternalStrength, Is.EqualTo(0.75 * (1 + Rules.RuneBonus)).Within(1e-9));
            loop.State.Golemancer.Runes = 100;
            Cast(loop, "golemancer_path_2");
            Assert.That(loop.State.Golemancer.Runes, Is.EqualTo(Rules.RunesMax));
        }

        [Test]
        public void EveryNewEnemyGetsABrandOfRuneLayersAndHitsStripThem()
        {
            var (loop, input) = Hero(Flat(GolemancerStats.EternalPower, 0.75));
            loop.State.Golemancer.Runes = 4;
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.State.Golemancer.Brand, Is.EqualTo(4));
            Hits(loop, input, 40);
            Assert.That(loop.State.Golemancer.Brand, Is.EqualTo(3));
            Assert.That(loop.State.Golemancer.BrandPops, Is.EqualTo(1));
        }

        [Test]
        public void InAPartyTheBrandHangsOnTheSharedEnemyAndAlliesStripIt()
        {
            var (loop, _) = Hero(Flat(GolemancerStats.EternalPower, 0.75));
            Party(loop, 2);
            loop.State.Golemancer.Runes = 12;
            Spawn(loop, EnemyType.Normal);
            loop.State.TraitEnemyEffects.Clear();
            loop.Tick();
            Assert.That(loop.State.Golemancer.Brand, Is.EqualTo(Rules.BrandMax));
            var found = false;
            for (var i = 0; i < loop.State.TraitEnemyEffects.Count; i++)
                found |= loop.State.TraitEnemyEffects[i].Id == GolemancerMechanics.BrandEffect;
            Assert.That(found, Is.True);
            loop.Combat.Golemancer.AddAllyHits(Rules.BrandHits * 2);
            Assert.That(loop.State.Golemancer.Brand, Is.EqualTo(Rules.BrandMax - 2));
        }

        [Test]
        public void TheHeavyFistAndTheStoneGuardian()
        {
            var (heavy, _) = Hero(Flat(GolemancerStats.EternalPower, 0.75), Flat(GolemancerStats.EternalHeavy, 1));
            heavy.Events.Clear();
            Wait(heavy, 2);
            Assert.That(Damage(heavy), Has.Count.EqualTo(1));
            var (boss, _) = Boss(Flat(GolemancerStats.EternalPower, 0.75), Flat(GolemancerStats.EternalBoss, 1));
            Assert.That(boss.Combat.Golemancer.EternalStrength, Is.EqualTo(0.75 * Rules.BossMultiplier).Within(1e-9));
        }

        [Test]
        public void EveryTwentyHitsShapeAGolemlingUpToFour()
        {
            var (loop, input) = Hero(Flat(GolemancerStats.MinorPower, 0.35));
            Hits(loop, input, 20);
            Assert.That(loop.State.Golemancer.Minors, Is.EqualTo(1));
            Cast(loop, "golemancer_path_3");
            Cast(loop, "golemancer_path_3");
            Assert.That(loop.State.Golemancer.Minors, Is.EqualTo(Rules.MinorMax));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            Wait(loop, Rules.MinorSeconds);
            Assert.That(loop.State.Golemancer.Minors, Is.Zero);
        }

        [Test]
        public void InAPartyGolemlingsGoToAllies()
        {
            var (loop, _) = Hero(Flat(GolemancerStats.MinorPower, 0.35));
            Party(loop, 2);
            loop.State.TraitRelays.Clear();
            Cast(loop, "golemancer_path_3");
            Assert.That(HasRelay(loop, TraitRelayKind.Golemling, out var golemling), Is.True);
            Assert.That(golemling.To, Is.EqualTo(LanternbearerMechanics.AnyAlly));
            Assert.That(loop.State.Golemancer.Minors, Is.Zero);
            Assert.That(loop.State.Golemancer.MinorsSent, Is.EqualTo(3));
        }

        [Test]
        public void AnAllysGolemlingStrikesAndCountsTowardTheirTraitCounters()
        {
            var (ally, _) = Hero(HeroClass.Soulbinder, EnemyType.Normal, Flat(SoulbinderStats.LendPower, 0.2));
            GolemancerMechanics.LandGolemling(ally.State.Boons, 0.35, 15);
            Wait(ally, 3.05);
            Assert.That(ally.State.Boons.GolemlingHits, Is.EqualTo(3));
            Assert.That(ally.State.Soulbinder.LendHits, Is.EqualTo(3));
        }

        [Test]
        public void TheWorkshopDoublesGolemlingsSolo()
        {
            var (loop, _) = Hero(Flat(GolemancerStats.MinorPower, 0.35), Flat(GolemancerStats.MinorSolo, 1));
            Assert.That(loop.Combat.Golemancer.MinorStrength, Is.EqualTo(0.7).Within(1e-9));
        }

        [Test]
        public void SkillsAddHitsToTheSealAndStrikeWithTheGolem()
        {
            var (loop, _) = Hero(Flat(GolemancerStats.SealPower, 3));
            Cast(loop, "golemancer_hit_1");
            Assert.That(loop.State.Golemancer.SealHits, Is.EqualTo(10));
            Cast(loop, "golemancer_path_1");
            Assert.That(Cast(loop, "golemancer_hit_3"), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheOldGolemOnFullClayStopsUnderTheNewTraitsAndTheRingIsTheWeapon()
        {
            var (loop, _) = Hero(Flat(GolemancerStats.MinorPower, 0.35));
            loop.State.Resource = loop.Resource.Max;
            loop.Tick();
            Assert.That(loop.Skills.Minions.Standing(MinionKind.Golem), Is.Zero);
            Assert.That(TestWorld.Real.Gems["golemancer_hit_1"].Skill!.Weapon, Is.EqualTo("seal_ring"));
        }
    }
}
