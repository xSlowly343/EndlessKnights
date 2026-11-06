using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CraftMasteryTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static SetDef Set(string id)
        {
            Assert.That(Items.TryGetSet(id, out var set), Is.True, id);
            return set;
        }

        private static GameLoop Runedancer() =>
            TestWorld.Loop(state: new GameState(7) { Class = HeroClass.Runedancer });

        private static void Collect(GameLoop loop, string setId)
        {
            foreach (var piece in Set(setId).Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(setId, piece));
            loop.Gear.Rebuild();
        }

        private static double StrikeDamage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 100, StatTags.Strike);

        [Test]
        public void MasteryGrowsEveryFiveHundredSpentAndSlowsAfterTwentyFiveLevels()
        {
            Assert.That(Items.MasteryLevel(0), Is.EqualTo(1));
            Assert.That(Items.MasteryLevel(499), Is.EqualTo(1));
            Assert.That(Items.MasteryLevel(500), Is.EqualTo(2));
            Assert.That(Items.MasteryLevel(4500), Is.EqualTo(10));
            Assert.That(Items.MasteryScale(1), Is.EqualTo(1));
            Assert.That(Items.MasteryScale(2), Is.EqualTo(1.04).Within(1e-12));
            Assert.That(Items.MasteryScale(26), Is.EqualTo(System.Math.Pow(1.04, 25)).Within(1e-9));
            Assert.That(Items.MasteryScale(27), Is.EqualTo(System.Math.Pow(1.04, 25) * 1.02).Within(1e-9));
        }

        [Test]
        public void MasteryRaisesEveryCollectedSetBonusAtOnce()
        {
            var loop = Runedancer();
            Collect(loop, "jarl_forge");
            Assert.That(StrikeDamage(loop), Is.EqualTo(108).Within(1e-9));

            loop.State.Drops.CraftSpent = 500;
            loop.Gear.Rebuild();

            Assert.That(loop.Gear.MasteryLevel, Is.EqualTo(2));
            Assert.That(StrikeDamage(loop), Is.EqualTo(100 * (1 + 0.08 * 1.04)).Within(1e-9));
        }

        [Test]
        public void CraftingRaisesMastery()
        {
            var loop = Runedancer();
            loop.State.Drops.Ingots = 1000;
            for (var i = 0; i < 3; i++)
                Assert.That(loop.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.Crafted));
            Assert.That(loop.State.Drops.CraftSpent, Is.EqualTo(15 + 18 + 21));
            Assert.That(loop.State.Drops.CraftSpent, Is.EqualTo(1000 - loop.State.Drops.Ingots));
            Assert.That(loop.Gear.MasteryLevel, Is.EqualTo(Items.MasteryLevel(loop.State.Drops.CraftSpent)));
        }

        [Test]
        public void EveryCollectedTopRaritySetMultipliesDamage()
        {
            var forge = Set("underside_forge");
            var loop = Runedancer();
            Collect(loop, "underside_forge");
            Assert.That(StrikeDamage(loop), Is.EqualTo(100 * (1 + forge.Bonus[0].Value) * 1.08).Within(1e-9));

            Collect(loop, "underside_mainstay");
            Collect(loop, Items.Sets.First(s => s.Rarity == ItemRarity.Mythic).Id);
            Collect(loop, Items.Sets.First(s => s.Rarity == ItemRarity.Legendary).Id);

            Assert.That(loop.Gear.CollectedSetsOf(ItemRarity.Rift), Is.EqualTo(2));
            Assert.That(loop.Gear.CollectionMore, Is.EqualTo(1.08 * 1.08 * 1.03).Within(1e-12), "legendary sets do not multiply");
            Assert.That(Items.CollectionMore(ItemRarity.Ancient, 34), Is.EqualTo(System.Math.Pow(1.05, 34)).Within(1e-9));
        }

        [Test]
        public void UniquesAreCraftedFromMasteryTenOnceFound()
        {
            var loop = Runedancer();
            loop.State.Drops.Ingots = 1000;
            loop.State.Drops.Essence = 1000;

            Assert.That(loop.CraftUnique("stenographer_gloves"), Is.EqualTo(CraftResult.MasteryTooLow));
            loop.State.Drops.CraftSpent = 4500;
            loop.Gear.Rebuild();
            Assert.That(loop.CraftUnique("stenographer_gloves"), Is.EqualTo(CraftResult.NotFound));

            loop.State.UniquesFound.Add("stenographer_gloves");
            Assert.That(loop.CraftUnique("stenographer_gloves"), Is.EqualTo(CraftResult.Crafted));
            Assert.That(loop.State.Drops.Gear.Last().Unique!.Id, Is.EqualTo("stenographer_gloves"));
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(850));
            Assert.That(loop.State.Drops.CraftSpent, Is.EqualTo(4650));

            loop.State.UniquesFound.Add("two_hands_ring");
            Assert.That(loop.CraftUnique("two_hands_ring"), Is.EqualTo(CraftResult.Crafted));
            Assert.That(loop.State.Drops.Essence, Is.EqualTo(700), "jewelry costs essence twice");

            loop.State.UniquesFound.Add("corsair_daggers");
            Assert.That(loop.CraftUnique("corsair_daggers"), Is.EqualTo(CraftResult.Crafted));
            Assert.That(loop.State.Weapons.Bag.Last().Unique!.Id, Is.EqualTo("corsair_daggers"));
        }

        [Test]
        public void MasteryAndFoundUniquesSurviveTheSave()
        {
            var loop = Runedancer();
            loop.State.Drops.CraftSpent = 1234;
            loop.State.UniquesFound.Add("silence_helm");

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Drops.CraftSpent, Is.EqualTo(1234));
            Assert.That(loaded.UniquesFound, Does.Contain("silence_helm"));
        }

        [Test]
        public void EveryEventHasAnArmourAndAJewelrySetThatNeverDropOrCraft()
        {
            var eventSets = Items.Sets.Where(s => s.Event != null).ToList();
            foreach (var seasonal in TestWorld.Data.Events.Events)
            {
                var own = eventSets.Where(s => s.Event == seasonal.Id).ToList();
                Assert.That(own.Count, Is.EqualTo(2), seasonal.Id);
                Assert.That(own.Single(s => s.Jewelry).Pieces.Count, Is.EqualTo(3), seasonal.Id);
                Assert.That(own.Single(s => !s.Jewelry).Slots, Is.EquivalentTo(new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots }), seasonal.Id);
            }
            Assert.That(eventSets.Count, Is.EqualTo(2 * TestWorld.Data.Events.Events.Count));
            Assert.That(eventSets.All(s => !s.Has(ItemSlot.Weapon)), Is.True);

            var rng = new Rng(9);
            foreach (var slot in new[] { ItemSlot.Helmet, ItemSlot.Amulet, ItemSlot.Ring })
            {
                for (var i = 0; i < 200; i++)
                    Assert.That(Items.PickSet(slot, ItemRarity.Legendary, rng)!.Event, Is.Null);
            }

            var loop = Runedancer();
            loop.State.Drops.Ingots = 1000;
            loop.State.Drops.Essence = 1000;
            Assert.That(loop.CraftSetPiece("pumpkin_night_armor"), Is.EqualTo(CraftResult.NotCraftable));
            Assert.That(loop.CraftSetPiece("pumpkin_night_jewelry"), Is.EqualTo(CraftResult.NotCraftable));
        }

        [Test]
        public void EventBossWinsGiveMissingPiecesOfThatEventSets()
        {
            var loop = Runedancer();
            Assert.That(loop.SeasonalEvents.BossWon, Is.Not.Null);
            var pieces = Set("pumpkin_night_armor").Pieces.Count + Set("pumpkin_night_jewelry").Pieces.Count;

            for (var i = 0; i < pieces; i++)
            {
                Assert.That(loop.Drops.GiveEventPiece("pumpkin_night"), Is.True);
                Assert.That(loop.State.Drops.Gear.Last().Set!.Event, Is.EqualTo("pumpkin_night"));
            }

            Assert.That(loop.Drops.GiveEventPiece("pumpkin_night"), Is.False);
            Assert.That(loop.Gear.Collected(Set("pumpkin_night_armor")), Is.True);
            Assert.That(loop.Gear.Collected(Set("pumpkin_night_jewelry")), Is.True);
            Assert.That(loop.State.SetCollection.Count(k => k.StartsWith("long_night")), Is.Zero);
        }
    }
}
