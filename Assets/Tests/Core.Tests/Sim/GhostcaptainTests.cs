using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GhostcaptainTests
    {
        private static GhostcaptainRules Rules => TestWorld.Data.Combat.Classes.Ghostcaptain;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Ghostcaptain, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Ghostcaptain, EnemyType.Boss, modifiers);

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
            loop.Combat.Ghostcaptain.OnSkillCast(TestWorld.Data.Gems[skill], 1);
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

        private static void Kill(GameLoop loop) => loop.Combat.Ghostcaptain.OnKillRewarded();

        [Test]
        public void EveryTenHitsLoadACannonAndEightFireABroadside()
        {
            Assert.That(Rules.Cannons, Is.EqualTo(8));
            Assert.That(Rules.CannonHits, Is.EqualTo(10));
            var (loop, input) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3));
            Hits(loop, input, 30);
            Assert.That(loop.State.Ghostcaptain.Loaded, Is.EqualTo(3));
            Hits(loop, input, 50);
            Assert.That(loop.State.Ghostcaptain.Volleys, Is.EqualTo(1));
            Assert.That(loop.State.Ghostcaptain.Loaded, Is.Zero);
        }

        [Test]
        public void ABroadsideLoadedOnOneEnemyIsTwiceAsStrong()
        {
            var (same, sameInput) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3));
            var (mixed, mixedInput) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3));
            Hits(same, sameInput, 79);
            Hits(mixed, mixedInput, 79);
            Spawn(mixed, EnemyType.Normal);
            same.Events.Clear();
            mixed.Events.Clear();
            Hits(same, sameInput, 1);
            Hits(mixed, mixedInput, 1);
            var sameVolley = Damage(same);
            var mixedVolley = Damage(mixed);
            sameVolley.Sort();
            mixedVolley.Sort();
            Assert.That(sameVolley[^1] / mixedVolley[^1], Is.EqualTo(Rules.SameEnemy).Within(1e-6));
        }

        [Test]
        public void ABroadsideLeavesABreachThatStopsTheBossTimerAndExposesTheEnemy()
        {
            var (loop, input) = Boss(Flat(GhostcaptainStats.BroadsidePower, 3));
            Assert.That(loop.Combat.Ghostcaptain.DamageTaken(), Is.EqualTo(1));
            Hits(loop, input, 80);
            Assert.That(loop.State.Ghostcaptain.BreachTicks, Is.GreaterThan(0));
            Assert.That(loop.State.BossTimerStopTicks, Is.GreaterThan(0));
            Assert.That(loop.Combat.Ghostcaptain.DamageTaken(), Is.EqualTo(1 + Rules.BreachDamageTaken).Within(1e-9));
        }

        [Test]
        public void InAPartyTheBreachHangsOnTheSharedEnemyAndStopsItsTimer()
        {
            var (loop, input) = Boss(Flat(GhostcaptainStats.BroadsidePower, 3));
            Party(loop, 2);
            Hits(loop, input, 80);
            var effects = loop.State.TraitEnemyEffects;
            var found = false;
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i].Id != GhostcaptainMechanics.BreachEffect)
                    continue;
                found = true;
                Assert.That(effects[i].Strength, Is.EqualTo(Rules.BreachDamageTaken).Within(1e-9));
                Assert.That(effects[i].TimerSlow, Is.EqualTo(1));
            }
            Assert.That(found, Is.True);
            Assert.That(loop.Combat.Ghostcaptain.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void TenCannonsLoadSlowerAndHalfCannonsFireEarly()
        {
            var (ten, _) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3), Flat(GhostcaptainStats.BroadsideCannons, 2), Flat(GhostcaptainStats.BroadsideHits, 2));
            Assert.That(ten.Combat.Ghostcaptain.Cannons, Is.EqualTo(10));
            Assert.That(ten.Combat.Ghostcaptain.CannonHits, Is.EqualTo(12));
            var (half, input) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3), Flat(GhostcaptainStats.BroadsideHalf, 1));
            Hits(half, input, 40);
            Assert.That(half.State.Ghostcaptain.Volleys, Is.EqualTo(1));
        }

        [Test]
        public void PowderMagazineAndLoadedCannonsAfterTheBroadside()
        {
            var (loop, input) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3), Flat(GhostcaptainStats.BroadsidePowder, 1), Flat(GhostcaptainStats.BroadsideLoaded, 1));
            Hits(loop, input, 80);
            Assert.That(loop.State.Ghostcaptain.Loaded, Is.EqualTo(Rules.LoadedAfter));
            Assert.That(loop.State.Resource, Is.GreaterThanOrEqualTo(Rules.VolleyPowder));
        }

        [Test]
        public void AllGunsFireLoadsEveryCannonAndFires()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.BroadsidePower, 3));
            Assert.That(Cast(loop, "ghostcaptain_path_1"), Is.Not.Empty);
            Assert.That(loop.State.Ghostcaptain.Volleys, Is.EqualTo(1));
            Cast(loop, "ghostcaptain_hit_1");
            Assert.That(loop.State.Ghostcaptain.Loaded, Is.EqualTo(2));
        }

        [Test]
        public void EveryKillJoinsTheCrewAndBossesBringThree()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03));
            Kill(loop);
            Kill(loop);
            Assert.That(loop.State.Ghostcaptain.Sailors, Is.EqualTo(2));
            var (boss, _) = Boss(Flat(GhostcaptainStats.CrewPower, 0.03));
            Kill(boss);
            Assert.That(boss.State.Ghostcaptain.Sailors, Is.EqualTo(Rules.BossSailors));
            var (press, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03), Flat(GhostcaptainStats.CrewDouble, 1));
            Kill(press);
            Assert.That(press.State.Ghostcaptain.Sailors, Is.EqualTo(2));
        }

        [Test]
        public void TheCrewStrikesEverySecondWithTheStrengthOfAllSailors()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03));
            for (var i = 0; i < 5; i++)
                Kill(loop);
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            var (one, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03));
            Kill(one);
            one.Events.Clear();
            Wait(one, 1);
            Assert.That(Damage(loop)[0] / Damage(one)[0], Is.EqualTo(5).Within(1e-6));
        }

        [Test]
        public void AtDawnTheCrewLeavesAndTheBoatswainFiresWithHerSkills()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03));
            for (var i = 0; i < 4; i++)
                Kill(loop);
            Wait(loop, Rules.DawnSeconds + 0.2);
            Assert.That(loop.State.Ghostcaptain.Sailors, Is.Zero);
            Assert.That(loop.State.Ghostcaptain.Dawns, Is.EqualTo(1));
            Assert.That(loop.State.Ghostcaptain.MateTicks, Is.GreaterThan(0));
            Cast(loop, "ghostcaptain_hit_2");
            Assert.That(loop.State.Ghostcaptain.MateShots, Is.EqualTo(1));
        }

        [Test]
        public void InAPartyTheBoatswainGoesToAnAlly()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03));
            Party(loop, 2);
            Kill(loop);
            loop.State.TraitRelays.Clear();
            Wait(loop, Rules.DawnSeconds);
            Assert.That(HasRelay(loop, TraitRelayKind.Mate, out var mate), Is.True);
            Assert.That(mate.To, Is.EqualTo(LanternbearerMechanics.AnyAlly));
            Assert.That(mate.Value, Is.EqualTo(0.03 * Rules.MateMultiplier).Within(1e-9));
            Assert.That(loop.State.Ghostcaptain.MateTicks, Is.Zero);
        }

        [Test]
        public void AnAllysBoatswainFiresWithEverySkillOfTheAlly()
        {
            var (ally, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            ally.State.Boons.MatePower = 0.3;
            ally.State.Boons.MateTicks = 100;
            Assert.That(Cast(ally, "runedancer_hit_1"), Has.Count.EqualTo(1));
            Assert.That(ally.State.Boons.MateShots, Is.EqualTo(1));
        }

        [Test]
        public void TheFarewellSaluteAndTheOldGuardKeepPartOfTheCrew()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03), Flat(GhostcaptainStats.CrewFarewell, 1), Flat(GhostcaptainStats.CrewKeep, 1));
            for (var i = 0; i < 8; i++)
                Kill(loop);
            Wait(loop, Rules.DawnSeconds - 0.55);
            loop.Events.Clear();
            Wait(loop, 0.7);
            var damage = Damage(loop);
            damage.Sort();
            Assert.That(damage, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(damage[^1], Is.GreaterThan(damage[0] * 2));
            Assert.That(loop.State.Ghostcaptain.Sailors, Is.EqualTo(2));
        }

        [Test]
        public void TheParrotRepeatsEveryThirdOwnSkillAtHalfStrength()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.ParrotPower, 0.5));
            Assert.That(Cast(loop, "ghostcaptain_hit_2"), Is.Empty);
            Assert.That(Cast(loop, "ghostcaptain_hit_2"), Is.Empty);
            var repeat = Cast(loop, "ghostcaptain_hit_2");
            Assert.That(repeat, Has.Count.EqualTo(1));
            Assert.That(loop.State.Ghostcaptain.Repeats, Is.EqualTo(1));
            var (chatty, _) = Hero(Flat(GhostcaptainStats.ParrotPower, 0.5), Flat(GhostcaptainStats.ParrotEvery, -1));
            Cast(chatty, "ghostcaptain_hit_2");
            Assert.That(Cast(chatty, "ghostcaptain_hit_2"), Has.Count.EqualTo(1));
        }

        [Test]
        public void SayItAgainRepeatsTheLastSkillAtOnce()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.ParrotPower, 0.5));
            Cast(loop, "ghostcaptain_hit_3");
            Assert.That(loop.State.Ghostcaptain.LastSkillPower, Is.EqualTo(5).Within(1e-9));
            Cast(loop, "ghostcaptain_path_3");
            Assert.That(loop.State.Ghostcaptain.Repeats, Is.EqualTo(1));
        }

        [Test]
        public void InAPartyTheParrotIsOfferedAndFliesToTheAllyWhoCalls()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.ParrotPower, 0.5));
            Party(loop, 3);
            loop.Tick();
            Assert.That(HasRelay(loop, TraitRelayKind.ParrotOffer, out var offer), Is.True);
            Assert.That(offer.To, Is.EqualTo(TraitRelay.AllSlots));
            loop.State.TraitRelays.Clear();
            loop.Combat.Ghostcaptain.ReceiveParrotCall(2);
            Assert.That(HasRelay(loop, TraitRelayKind.ParrotPerch, out var perch), Is.True);
            Assert.That(perch.To, Is.EqualTo(2));
            Assert.That(perch.Value, Is.EqualTo(0.5).Within(1e-9));
            loop.State.TraitRelays.Clear();
            loop.Combat.Ghostcaptain.ReceiveParrotCall(1);
            var perches = 0;
            for (var i = 0; i < loop.State.TraitRelays.Count; i++)
            {
                if (loop.State.TraitRelays[i].Kind == TraitRelayKind.ParrotPerch)
                    perches++;
            }
            Assert.That(perches, Is.EqualTo(2));
            Assert.That(loop.State.Ghostcaptain.PerchSlot, Is.EqualTo(1));
            Assert.That(Cast(loop, "ghostcaptain_hit_2"), Is.Empty);
        }

        [Test]
        public void AnAllyCallsTheParrotWithASkillAndThePerchedParrotRepeatsTheirThirdSkill()
        {
            var (ally, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            ally.State.Boons.ParrotOwner = 0;
            ally.State.Boons.ParrotOfferTicks = 100;
            Cast(ally, "runedancer_hit_1");
            ally.State.TraitRelays.Clear();
            ally.Tick();
            Assert.That(HasRelay(ally, TraitRelayKind.ParrotCall, out var call), Is.True);
            Assert.That(call.To, Is.EqualTo(0));
            ally.State.Boons.ParrotPower = 0.5;
            ally.State.Boons.ParrotEvery = 3;
            ally.State.Boons.ParrotTicks = 100;
            Cast(ally, "runedancer_hit_1");
            Cast(ally, "runedancer_hit_1");
            Assert.That(Cast(ally, "runedancer_hit_1"), Has.Count.EqualTo(1));
            Assert.That(ally.State.Boons.ParrotRepeats, Is.EqualTo(1));
        }

        [Test]
        public void TheOldMouseBroadsideStopsUnderTheNewTraits()
        {
            var (loop, _) = Hero(Flat(GhostcaptainStats.CrewPower, 0.03));
            Assert.That(loop.Combat.Ghostcaptain.Traited, Is.True);
            var (plain, _) = Hero();
            Assert.That(plain.Combat.Ghostcaptain.Traited, Is.False);
            var (other, _) = Hero(HeroClass.Runedancer, EnemyType.Normal, Flat(GhostcaptainStats.CrewPower, 0.03));
            Assert.That(other.Combat.Ghostcaptain.Traited, Is.False);
        }

        [Test]
        public void CompassShotIsTheCaptainsStartingSkill()
        {
            Assert.That(TestWorld.Real.SkillBook.StartingGems("ghostcaptain"), Has.Some.Property("Id").EqualTo("ghostcaptain_hit_1"));
            Assert.That(TestWorld.Real.Gems["ghostcaptain_hit_1"].Skill!.Weapon, Is.EqualTo("compass_pistol"));
        }
    }
}
