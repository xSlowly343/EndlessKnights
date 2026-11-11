using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionNestTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;
        private static NestRules Rules => TestWorld.Data.Companions.Nest;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void HostsDropEggsRarelyAndAtMostOnceAWeek()
        {
            var eggs = 0;
            var guardian = TestWorld.Real.Enemies.All.First(enemy => enemy.Type == EnemyType.Guardian);
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                var loop = Camp(seed);
                var first = loop.Events.Count;
                loop.Events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, 0, 50, guardian));
                loop.Nest.NoteGuardians(first);
                eggs += loop.State.Companions.Eggs.Count;
                if (loop.State.Companions.Eggs.Count > 0)
                {
                    for (var i = 0; i < 200; i++)
                    {
                        var next = loop.Events.Count;
                        loop.Events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, 0, 50, guardian));
                        loop.Nest.NoteGuardians(next);
                    }
                    Assert.That(loop.State.Companions.Eggs.Count, Is.EqualTo(1), "one egg a week");
                }
            }
            Assert.That(eggs, Is.InRange(3, 25));
        }

        [Test]
        public void AnEggHatchesInADayIntoTheCompanionOfItsBiome()
        {
            var loop = Camp();
            loop.Nest.Lay("ice_peak");
            loop.Nest.Advance(23 * Hour);
            Assert.That(loop.State.Companions.Eggs.Count, Is.EqualTo(1));
            loop.Events.Clear();
            loop.Nest.Advance(Hour);
            Assert.That(loop.State.Companions.Eggs, Is.Empty);
            Assert.That(loop.Companions.Known("egg_mammoth"), Is.True);
            Assert.That(loop.Events.CountOf(GameEventKind.CompanionHatched), Is.EqualTo(1));
        }

        [Test]
        public void OnlyEggsInNestSlotsWarm()
        {
            var loop = Camp();
            Assert.That(loop.Nest.Slots, Is.EqualTo(1));
            loop.Nest.Lay("ice_peak");
            loop.Nest.Lay("sky_islands");
            loop.Nest.Advance(24 * Hour);
            Assert.That(loop.State.Companions.Eggs.Count, Is.EqualTo(1));
            Assert.That(loop.State.Companions.Eggs[0].SecondsLeft, Is.EqualTo(Rules.HatchSeconds));
        }

        [Test]
        public void EggsWaitingInTheQueueWarmAsSoonAsASlotIsFree()
        {
            var loop = Camp();
            Assert.That(loop.Nest.Slots, Is.EqualTo(1));
            loop.Nest.Lay("ice_peak");
            loop.Nest.Lay("sky_islands");
            loop.Nest.Lay("green_hills");

            loop.Nest.Advance(Rules.HatchSeconds * 2 + 6 * Hour);

            Assert.That(loop.State.Companions.Hatched, Is.EqualTo(2));
            Assert.That(loop.State.Companions.Eggs.Count, Is.EqualTo(1));
            Assert.That(loop.State.Companions.Eggs[0].SecondsLeft, Is.EqualTo(Rules.HatchSeconds - 6 * Hour).Within(1e-6));
        }

        [Test]
        public void TheMenagerieTreeWarmsTheNestAndCheapensTheChoiceEgg()
        {
            var loop = Camp();
            loop.State.Companions.MenagerieLevel = 60;
            for (var i = 0; i < 3; i++)
                loop.Menagerie.Learn(6);
            for (var node = 1; node <= 2; node++)
                for (var i = 0; i < 5; i++)
                    loop.Menagerie.Learn(node);
            for (var i = 0; i < 5; i++)
                Assert.That(loop.Menagerie.Learn(17), Is.EqualTo(TreeResult.Done));
            loop.Nest.Lay("green_hills");
            var before = loop.State.Companions.Eggs[0].SecondsLeft;
            loop.Camp.Add("ember_coal", 1);

            Assert.That(loop.Nest.AddCoal(0), Is.EqualTo(NestResult.Done));

            Assert.That(before - loop.State.Companions.Eggs[0].SecondsLeft, Is.EqualTo(Rules.CoalSeconds * 1.2).Within(1e-6));
            Assert.That(loop.State.Companions.CoalToday, Is.EqualTo(Rules.CoalSeconds).Within(1e-6), "the daily limit counts plain coal hours");
            Assert.That(loop.Nest.ChoiceSparks, Is.EqualTo(Rules.SparksForChoice - 1));
            loop.State.Companions.Sparks = Rules.SparksForChoice - 1;
            Assert.That(loop.Nest.ChoiceEgg("ice_peak"), Is.EqualTo(NestResult.Done));
            Assert.That(loop.State.Companions.Sparks, Is.Zero);
        }

        [Test]
        public void CoalWarmsUpToSixHoursADay()
        {
            var loop = Camp();
            loop.Nest.Lay("green_hills");
            Assert.That(loop.Nest.AddCoal(0), Is.EqualTo(NestResult.NoCoal));
            loop.Camp.Add("ember_coal", 10);
            for (var i = 0; i < 6; i++)
                Assert.That(loop.Nest.AddCoal(0), Is.EqualTo(NestResult.Done));
            Assert.That(loop.Nest.AddCoal(0), Is.EqualTo(NestResult.CoalLimit));
            Assert.That(loop.State.Companions.Eggs[0].SecondsLeft, Is.EqualTo(18 * Hour).Within(1e-6));
            loop.SetLocalDay(101);
            Assert.That(loop.Nest.AddCoal(0), Is.EqualTo(NestResult.Done));
            Assert.That(loop.Nest.FriendWarm(0), Is.EqualTo(NestResult.Done));
            Assert.That(loop.State.Companions.Eggs[0].SecondsLeft, Is.EqualTo(15 * Hour).Within(1e-6));
        }

        [Test]
        public void ARepeatTurnsIntoASparkAndFiveSparksBuyAnEggOfChoice()
        {
            var loop = Camp();
            loop.Companions.Grant("egg_boarlet");
            loop.Nest.Lay("green_hills");
            loop.Nest.HatchNow(0);
            Assert.That(loop.State.Companions.Sparks, Is.EqualTo(1));
            Assert.That(loop.Nest.UseSpark("camp_dog"), Is.EqualTo(NestResult.Done));
            Assert.That(loop.Companions.LevelOf("camp_dog"), Is.EqualTo(3));
            Assert.That(loop.Nest.ChoiceEgg("ice_peak"), Is.EqualTo(NestResult.NoSparks));
            loop.State.Companions.Sparks = 5;
            Assert.That(loop.Nest.ChoiceEgg("green_hills"), Is.EqualTo(NestResult.BiomeTaken));
            Assert.That(loop.Nest.ChoiceEgg("ice_peak"), Is.EqualTo(NestResult.Done));
            Assert.That(loop.Nest.ChoiceEgg("ice_peak"), Is.EqualTo(NestResult.BiomeTaken), "an egg of that biome is already waiting");
            Assert.That(loop.State.Companions.Sparks, Is.EqualTo(0));
        }

        [Test]
        public void TheMuseumAndTheHeartGiveTheirCompanions()
        {
            var loop = Camp();
            loop.State.Museum.Done.Add("colossus_bones");
            loop.Nest.Tick();
            Assert.That(loop.Companions.Known("museum_mole"), Is.True);
            Assert.That(loop.Companions.Known("heart_fold"), Is.False);
            loop.State.RecordStage = TestWorld.Data.Stages.HeartFromStage;
            loop.Nest.Tick();
            Assert.That(loop.Companions.Known("heart_fold"), Is.EqualTo(TestWorld.Data.Stages.Heart.Length > 0));
        }

        [Test]
        public void TheShopOffersThreeMissingEggCompanionsAWeek()
        {
            var loop = Camp();
            var offers = loop.Nest.ShopOffers();
            Assert.That(offers.Count, Is.EqualTo(3));
            Assert.That(offers.Distinct().Count(), Is.EqualTo(3));
            Assert.That(loop.Nest.ShopOffers(), Is.EqualTo(offers), "the same all week");
            Assert.That(loop.Nest.ShopBought("camp_dog"), Is.EqualTo(NestResult.NotOffered));
            Assert.That(loop.Nest.ShopBought(offers[0]), Is.EqualTo(NestResult.Done));
            Assert.That(loop.Nest.ShopOffers(), Does.Not.Contain(offers[0]));
        }

        [Test]
        public void EggsAndSparksSurviveTheSave()
        {
            var loop = Camp();
            loop.Nest.Lay("sky_islands");
            loop.State.Companions.Sparks = 2;
            loop.State.Companions.EggCooldown = 1000;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Eggs[0].Biome, Is.EqualTo("sky_islands"));
            Assert.That(copy.Companions.Sparks, Is.EqualTo(2));
            Assert.That(copy.Companions.EggCooldown, Is.EqualTo(1000));
        }
    }
}
