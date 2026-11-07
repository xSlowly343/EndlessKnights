using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionStarTests
    {
        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        private static void Awaken(GameLoop loop, string companion)
        {
            var progress = loop.Companions.Progress(companion);
            progress.Level = 30;
            progress.Stage = loop.Companions.Rules.Stages.Count - 1;
        }

        [Test]
        public void StarsNeedTheAwakenedStageAndCostSparksOrSouls()
        {
            var loop = Camp();
            Assert.That(loop.Companions.RaiseStar("camp_dog", true), Is.EqualTo(CompanionResult.NotAwakened));
            Awaken(loop, "camp_dog");
            Assert.That(loop.Companions.RaiseStar("camp_dog", true), Is.EqualTo(CompanionResult.NoSouls));
            loop.Companions.AddSouls(10);
            Assert.That(loop.Companions.RaiseStar("camp_dog", true), Is.EqualTo(CompanionResult.Done));
            loop.Companions.Progress("camp_dog").OwnSparks = 10;
            Assert.That(loop.Companions.RaiseStar("camp_dog", false), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.StarsOf("camp_dog"), Is.EqualTo(2));
            Assert.That(loop.Companions.MaxLevelOf("camp_dog"), Is.EqualTo(40));
            loop.Companions.Progress("camp_dog").Stars = 6;
            Assert.That(loop.Companions.RaiseStar("camp_dog", true), Is.EqualTo(CompanionResult.MaxStars));
            Assert.That(loop.Companions.MaxLevelOf("camp_dog"), Is.EqualTo(60));
        }

        [Test]
        public void StarsRaiseTheLevelCapAndTraitNumbers()
        {
            var loop = Camp();
            loop.Companions.Grant("jackal_pup");
            Awaken(loop, "jackal_pup");
            loop.Companions.Gain("jackal_pup", 10_000);
            Assert.That(loop.Companions.LevelOf("jackal_pup"), Is.EqualTo(30), "no stars, no levels above 30");
            loop.Companions.Progress("jackal_pup").Stars = 1;
            loop.Companions.Gain("jackal_pup", 10_000);
            Assert.That(loop.Companions.LevelOf("jackal_pup"), Is.EqualTo(35));
            Assert.That(loop.Companions.OwnMultiplier("jackal_pup", OwnEffectKind.Shards), Is.EqualTo(1 + (1.3 * 1.05 - 1) * 1.1).Within(1e-9), "the jackal branch adds its coin nose");
        }

        [Test]
        public void RarityGivesStartingStarsOnAwakening()
        {
            var loop = Camp();
            loop.Companions.Grant("jackal_pup");
            var jackal = loop.Companions.Progress("jackal_pup");
            jackal.Level = 30;
            jackal.Stage = 2;
            loop.Camp.Add("herb_colossus_bloom", 3);
            loop.State.Museum.Done.Add("fish_ruins");
            Assert.That(loop.Companions.Evolve("jackal_pup"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.StarsOf("jackal_pup"), Is.EqualTo(1), "a rare companion wakes with one star");
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 30;
            dog.Stage = 2;
            loop.Camp.Add("herb_colossus_bloom", 3);
            loop.State.Museum.Done.Add("fish_hills");
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.StarsOf("camp_dog"), Is.Zero);
        }

        [Test]
        public void SoulsComeFromHostsTripsAndThePartyTrip()
        {
            var loop = Camp();
            var host = TestWorld.Real.Enemies.All.First(enemy => enemy.Type == EnemyType.Guardian);
            for (var i = 0; i < 3; i++)
            {
                var first = loop.Events.Count;
                loop.Events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, 0, 50, host));
                loop.Nest.NoteGuardians(first);
            }
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(1), "one soul a day from hosts");
            loop.Companions.NoteTripHours(45);
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(3));
            loop.Hideout.Receive(new Dictionary<string, long> { ["companion_soul"] = 5 });
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(8));
        }

        [Test]
        public void ARepeatEggGivesASparkOfItsCompanion()
        {
            var loop = Camp();
            loop.Companions.Grant("egg_griffin");
            loop.Nest.Lay("sky_islands");
            loop.Nest.HatchNow(0);
            Assert.That(loop.Companions.Progress("egg_griffin").OwnSparks, Is.EqualTo(1));
        }

        [Test]
        public void StarsSurviveTheSave()
        {
            var loop = Camp();
            loop.Companions.Progress("camp_dog").Stars = 2;
            loop.Companions.Progress("camp_dog").OwnSparks = 4;
            loop.Companions.AddSouls(7);
            var copy = Game.Core.Save.GameStateJson.Read(new ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Stars, Is.EqualTo(2));
            Assert.That(copy.Companions.Progress["camp_dog"].OwnSparks, Is.EqualTo(4));
            Assert.That(copy.Companions.Souls, Is.EqualTo(7));
        }
    }
}
