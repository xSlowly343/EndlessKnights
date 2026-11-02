using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MothcallerTests
    {
        private static MothcallerRules Rules => TestWorld.Data.Combat.Classes.Mothcaller;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Mothcaller, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Mothcaller, EnemyType.Boss, modifiers);

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
            loop.Combat.Mothcaller.OnSkillCast(TestWorld.Data.Gems[skill]);
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

        private static void Kill(GameLoop loop) => loop.Combat.Mothcaller.OnKillRewarded();

        [Test]
        public void EveryHitReleasesAMothUpToFifty()
        {
            Assert.That(Rules.SwarmMax, Is.EqualTo(50));
            var (loop, input) = Hero(Flat(MothcallerStats.SwarmPower, 0.02));
            Hits(loop, input, 12);
            Assert.That(loop.State.Mothcaller.Moths, Is.EqualTo(12));
            loop.State.Mothcaller.Moths = 49;
            Hits(loop, input, 3);
            Assert.That(loop.State.Mothcaller.Moths, Is.EqualTo(50));
        }

        [Test]
        public void TheSwarmDrinksOncePerSecondWithTheStrengthOfEveryMoth()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02));
            loop.State.Mothcaller.Moths = 40;
            loop.Events.Clear();
            Wait(loop, 1);
            var forty = Damage(loop);
            Assert.That(forty, Has.Count.EqualTo(1));
            loop.State.Mothcaller.Moths = 10;
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(forty[0] / Damage(loop)[0], Is.EqualTo(4).Within(1e-6));
        }

        [Test]
        public void HalfTheSwarmMovesOnWhenTheEnemyDies()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02));
            loop.State.Mothcaller.Moths = 40;
            Kill(loop);
            Assert.That(loop.State.Mothcaller.Moths, Is.EqualTo(20));
            var (loyal, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02), Flat(MothcallerStats.SwarmKeep, 1));
            loyal.State.Mothcaller.Moths = 40;
            Kill(loyal);
            Assert.That(loyal.State.Mothcaller.Moths, Is.EqualTo(30));
            var (quick, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02), Flat(MothcallerStats.SwarmNoCarry, 1));
            quick.State.Mothcaller.Moths = 40;
            Kill(quick);
            Assert.That(quick.State.Mothcaller.Moths, Is.Zero);
        }

        [Test]
        public void GoldenMothsStrikeThreeTimesAsHard()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02), Flat(MothcallerStats.SwarmGolden, 1));
            loop.State.Mothcaller.Moths = 20;
            Assert.That(loop.Combat.Mothcaller.EffectiveMoths, Is.EqualTo(24).Within(1e-9));
        }

        [Test]
        public void InAPartyTheScatteredHalfGoesToTheAllyWhoFinishedTheEnemy()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02));
            Party(loop, 3);
            loop.State.Mothcaller.Moths = 40;
            Kill(loop);
            Assert.That(loop.State.Mothcaller.Moths, Is.EqualTo(40));
            loop.Combat.Mothcaller.OnPartyKill(2, 0);
            Assert.That(loop.State.Mothcaller.Moths, Is.EqualTo(20));
            Assert.That(HasRelay(loop, TraitRelayKind.SwarmGift, out var gift), Is.True);
            Assert.That(gift.To, Is.EqualTo(2));
            Assert.That(gift.Count, Is.EqualTo(20));
            loop.State.TraitRelays.Clear();
            loop.Combat.Mothcaller.OnPartyKill(0, 0);
            Assert.That(HasRelay(loop, TraitRelayKind.SwarmGift, out _), Is.False);
        }

        [Test]
        public void AnAllysGiftedMothsDrinkTheirEnemyEverySecond()
        {
            var (ally, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            ally.State.Boons.GiftMoths = 20;
            ally.State.Boons.GiftPower = 0.02;
            ally.State.Boons.GiftTicks = 100;
            ally.Events.Clear();
            Wait(ally, 1);
            Assert.That(Damage(ally), Has.Count.EqualTo(1));
            Assert.That(ally.State.Boons.GiftHits, Is.EqualTo(1));
        }

        [Test]
        public void TheGreedySwarmDrinksBossesTwiceAndTheHungrySwarmDwindles()
        {
            var (boss, _) = Boss(Flat(MothcallerStats.SwarmPower, 0.02), Flat(MothcallerStats.SwarmBoss, 1));
            var (plain, _) = Boss(Flat(MothcallerStats.SwarmPower, 0.02));
            boss.State.Mothcaller.Moths = 10;
            plain.State.Mothcaller.Moths = 10;
            boss.Events.Clear();
            plain.Events.Clear();
            Wait(boss, 1);
            Wait(plain, 1);
            Assert.That(Damage(boss)[0] / Damage(plain)[0], Is.EqualTo(Rules.SwarmBoss).Within(1e-6));
            var (hungry, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02), Flat(MothcallerStats.SwarmDecay, 1));
            hungry.State.Mothcaller.Moths = 10;
            Wait(hungry, 3);
            Assert.That(hungry.State.Mothcaller.Moths, Is.EqualTo(7));
        }

        [Test]
        public void PollenSettlesOnHerSoloAndACritShakesItOffOverTime()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.PollenPower, 0.02));
            loop.State.Mothcaller.Moths = 50;
            Wait(loop, 3);
            Assert.That(loop.State.Boons.Pollen, Is.GreaterThan(1));
            loop.Combat.Mothcaller.AfterStrike(true);
            Assert.That(loop.State.Boons.Pollen, Is.Zero);
            Assert.That(loop.State.Boons.PollenBurnTicks, Is.EqualTo((int)(Rules.PollenBurnSeconds * GameLoop.TicksPerSecond)));
            loop.Events.Clear();
            Wait(loop, Rules.PollenBurnSeconds);
            Assert.That(Damage(loop), Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(loop.State.Boons.PollenShakes, Is.EqualTo(1));
        }

        [Test]
        public void HerOwnMothsDoNotStrikeUnderPollen()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.PollenPower, 0.02));
            loop.State.Mothcaller.Moths = 50;
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Is.Empty);
        }

        [Test]
        public void InAPartyPollenSettlesOnEveryAlly()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.PollenPower, 0.02));
            Party(loop, 2);
            loop.State.Mothcaller.Moths = 50;
            loop.State.TraitRelays.Clear();
            Wait(loop, 1);
            Assert.That(HasRelay(loop, TraitRelayKind.Pollen, out var pollen), Is.True);
            Assert.That(pollen.To, Is.EqualTo(TraitRelay.AllSlots));
            Assert.That(pollen.Value, Is.EqualTo(1).Within(1e-9));
            Assert.That(pollen.Extra, Is.EqualTo(Rules.PollenBurnSeconds));
            Assert.That(loop.State.Boons.Pollen, Is.Zero);
        }

        [Test]
        public void HeavyPollenWaitsForEverySecondCritAndMoonPollenGivesResource()
        {
            var (ally, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            MothcallerMechanics.LandPollen(ally.State.Boons, 2, 10, 4, 2, 0, 0, 5);
            Wait(ally, 2);
            ally.Combat.Mothcaller.AfterStrike(true);
            Assert.That(ally.State.Boons.Pollen, Is.GreaterThan(0));
            ally.Combat.Mothcaller.AfterStrike(true);
            Assert.That(ally.State.Boons.Pollen, Is.Zero);
            Assert.That(ally.State.PendingResource, Is.EqualTo(5).Within(1e-9));
        }

        [Test]
        public void TheNightMothWakesEveryThirtySecondsForEight()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.MothPower, 4));
            Wait(loop, Rules.MothEverySeconds - 1);
            Assert.That(loop.State.Mothcaller.Wakes, Is.Zero);
            Wait(loop, 1.2);
            Assert.That(loop.State.Mothcaller.Wakes, Is.EqualTo(1));
            Assert.That(loop.Combat.Mothcaller.Awake, Is.True);
            loop.Events.Clear();
            Wait(loop, Rules.MothAwakeSeconds);
            Assert.That(Damage(loop), Has.Count.InRange(7, 9));
            Assert.That(loop.Combat.Mothcaller.Awake, Is.False);
        }

        [Test]
        public void TheMothIgnoresHitsAndNoMothsAreReleased()
        {
            var (loop, input) = Hero(Flat(MothcallerStats.MothPower, 4));
            Hits(loop, input, 10);
            Assert.That(loop.State.Mothcaller.Moths, Is.Zero);
        }

        [Test]
        public void AJointStreakOfTheWholePartyBringsTheWakingCloser()
        {
            var (solo, soloInput) = Hero(Flat(MothcallerStats.MothPower, 4));
            var (party, partyInput) = Hero(Flat(MothcallerStats.MothPower, 4));
            Party(party, 2);
            for (var i = 0; i < 50; i++)
            {
                party.State.PartyAllHitting = true;
                soloInput.PressKeys(1);
                partyInput.PressKeys(1);
                solo.Tick();
                party.Tick();
            }
            Assert.That(party.Combat.Mothcaller.SecondsToWake, Is.LessThan(solo.Combat.Mothcaller.SecondsToWake - 4));
            Assert.That(HasRelay(party, TraitRelayKind.MothClock, out var clock), Is.True);
            Assert.That(clock.To, Is.EqualTo(TraitRelay.AllSlots));
        }

        [Test]
        public void TheMothTakesASecondFromTheBossTimerAndWakingTheMothAtOnce()
        {
            var (loop, _) = Boss(Flat(MothcallerStats.MothPower, 4), Flat(MothcallerStats.MothTime, 1));
            var left = loop.State.BossTicksLeft;
            Cast(loop, "mothcaller_path_3");
            Assert.That(loop.State.Mothcaller.Wakes, Is.EqualTo(1));
            Assert.That(loop.State.BossTicksLeft, Is.EqualTo(left + GameLoop.TicksPerSecond));
        }

        [Test]
        public void TheMothWakesForABossAndItsWingDustStrengthensHits()
        {
            var (loop, _) = Boss(Flat(MothcallerStats.MothPower, 4), Flat(MothcallerStats.MothBoss, 1), Flat(MothcallerStats.MothStrike, 1));
            loop.Tick();
            Assert.That(loop.Combat.Mothcaller.Awake, Is.True);
            Assert.That(loop.Combat.Mothcaller.StrikeMultiplier(), Is.EqualTo(1 + Rules.MothStrike));
        }

        [Test]
        public void SkillsReleaseMothsAndStrikeWithTheSwarm()
        {
            var (loop, _) = Hero(Flat(MothcallerStats.SwarmPower, 0.02));
            Cast(loop, "mothcaller_hit_1");
            Assert.That(loop.State.Mothcaller.Moths, Is.EqualTo(5));
            Assert.That(Cast(loop, "mothcaller_hit_3"), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheMoonFanIsTheWeapon()
        {
            Assert.That(TestWorld.Real.Gems["mothcaller_hit_1"].Skill!.Weapon, Is.EqualTo("moon_fan"));
        }
    }
}
