using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionAlbumTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        private static readonly string[] Free = { "egg_boarlet", "egg_scarab", "egg_mammoth", "egg_wormlet", "egg_griffin", "museum_mole", "heart_fold" };

        private static void Grow(GameLoop loop, string companion, int stage, bool hearts)
        {
            loop.Companions.Grant(companion);
            var progress = loop.Companions.Progress(companion);
            progress.Stage = stage;
            if (hearts)
                progress.Bond = loop.Companions.Rules.Bond.PointsTo(loop.Companions.Rules.Bond.MaxHearts);
        }

        [Test]
        public void AStarForEachStageAndOneForTenHearts()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Stars("camp_dog"), Is.EqualTo(1));
            Assert.That(loop.Companions.Stars("egg_griffin"), Is.Zero, "an unknown companion has no stars");
            Grow(loop, "camp_dog", 3, true);
            Assert.That(loop.Companions.Stars("camp_dog"), Is.EqualTo(5));
            Assert.That(loop.Companions.AlbumStars(), Is.EqualTo(6));
            Assert.That(loop.Companions.MaxAlbumStars(true), Is.EqualTo(55 * 8), "all bonuses are free");
        }

        [Test]
        public void TheStarsOpenTripSlotsFoodNestAndTitle()
        {
            var loop = Camp();
            Assert.That(loop.Companions.TripSlots, Is.EqualTo(3));
            Grow(loop, "camp_dog", 3, true);
            Grow(loop, "camp_crow", 3, false);
            Assert.That(loop.Companions.AlbumStars(true), Is.EqualTo(9));
            Assert.That(loop.Companions.TripSlots, Is.EqualTo(3));
            Grow(loop, "egg_boarlet", 0, false);
            Assert.That(loop.Companions.TripSlots, Is.EqualTo(4));
            var nest = loop.Nest.Slots;
            foreach (var id in Free)
                Grow(loop, id, 3, true);
            Grow(loop, "camp_crow", 3, true);
            Assert.That(loop.Companions.AlbumStars(true), Is.EqualTo(45));
            Assert.That(loop.Companions.AlbumBonus(AlbumBonusKind.FoodExperience), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(loop.Nest.Slots, Is.EqualTo(nest + 1));
            Assert.That(loop.Museum.SetTitle("menagerie_master"), Is.EqualTo(MuseumResult.NotCollected));
            foreach (var id in new[] { "pumpkin_companion", "longnight_companion", "flood_companion", "solstice_companion" })
                Grow(loop, id, 3, true);
            Assert.That(loop.Companions.AlbumStars(true), Is.EqualTo(65));
            Assert.That(loop.Companions.TripSlots, Is.EqualTo(5));
            Assert.That(loop.Museum.SetTitle("menagerie_master"), Is.EqualTo(MuseumResult.Done));
        }

        [Test]
        public void PaidCompanionsAddStarsButNoBonuses()
        {
            var loop = Camp();
            Grow(loop, "pass1_companion", 3, true);
            Assert.That(loop.Companions.AlbumStars(), Is.EqualTo(7));
            Assert.That(loop.Companions.AlbumStars(true), Is.EqualTo(2));
        }

        [Test]
        public void TripsWaitForAFreeSlot()
        {
            var loop = Camp();
            foreach (var id in new[] { "egg_boarlet", "egg_scarab" })
                loop.Companions.Grant(id);
            loop.State.Expeditions.Active.Add(new Expedition { Companion = "x1", Biome = "green_hills", Hours = 4, Seconds = 100, Planned = 100, Items = new long[0] });
            loop.State.Expeditions.Active.Add(new Expedition { Companion = "x2", Biome = "green_hills", Hours = 4, Seconds = 100, Planned = 100, Items = new long[0] });
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 4), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.Expeditions.Send("camp_crow", "green_hills", 4), Is.EqualTo(ExpeditionResult.NoTripSlot));
        }

        [Test]
        public void FoodGivesMoreWithTwentyFiveStars()
        {
            var loop = Camp();
            foreach (var id in Free.Take(5))
                Grow(loop, id, 3, true);
            Assert.That(loop.Companions.AlbumStars(true), Is.EqualTo(27));
            loop.Camp.Add("herb_knockbloom", 1);
            loop.Companions.Feed("camp_crow", "herb_knockbloom");
            Assert.That(loop.Companions.Progress("camp_crow").Experience, Is.EqualTo(2 * 1.1).Within(1e-9));
        }

        [Test]
        public void TheAlbumShowsSilhouettesAndTheNextBonus()
        {
            var loop = Camp();
            var album = CompanionView.Album(loop.Companions);
            Assert.That(album.Cards.Count, Is.EqualTo(loop.Companions.Rules.Companions.Count));
            var griffin = album.Cards.Single(card => card.Companion == "egg_griffin");
            Assert.That(griffin.Known, Is.False);
            Assert.That(griffin.Card, Is.Null);
            Assert.That(griffin.Source, Is.EqualTo("egg"));
            Assert.That(griffin.Biome, Is.EqualTo("sky_islands"));
            Assert.That(album.Cards.Single(card => card.Companion == "camp_dog").Card!.Level, Is.EqualTo(1));
            Assert.That(album.Cards.Single(card => card.Companion == "pet_owl").Paid, Is.True);
            Assert.That(album.NextStars, Is.EqualTo(10));
            Assert.That(album.NextBonus, Is.EqualTo("tripSlot"));
        }
    }
}
