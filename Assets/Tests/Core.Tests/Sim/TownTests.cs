using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TownTests
    {
        private static TownRules Rules => TestWorld.Data.Town;
        private const long Day = 20_400;

        private static GameLoop Town(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(Day);
            return loop;
        }

        [Test]
        public void ThreeOrdersADayThatPileUpToTheLimitWithoutPenalty()
        {
            var loop = Town();
            Assert.That(loop.State.Town.Orders.Count, Is.EqualTo(Rules.OrdersPerDay));
            loop.SetLocalDay(Day);
            Assert.That(loop.State.Town.Orders.Count, Is.EqualTo(Rules.OrdersPerDay), "the same day adds nothing");
            loop.SetLocalDay(Day + 1);
            Assert.That(loop.State.Town.Orders.Count, Is.EqualTo(System.Math.Min(Rules.OrdersKept, 2 * Rules.OrdersPerDay)));
            loop.SetLocalDay(Day + 10);
            Assert.That(loop.State.Town.Orders.Count, Is.EqualTo(Rules.OrdersKept));
            Assert.That(loop.State.Town.Orders.Select(o => o.Request).Distinct().Count(), Is.EqualTo(loop.State.Town.Orders.Count), "no request twice on the board");
        }

        [Test]
        public void OnlyRequestsThePlayerCanMeetAreOffered()
        {
            for (var seed = 1UL; seed <= 20; seed++)
            {
                var loop = Town(seed);
                loop.SetLocalDay(Day + 5);
                foreach (var order in loop.State.Town.Orders)
                {
                    var request = Rules.Request(order.Request)!;
                    Assert.That(request.Biome, Is.Empty, "a fresh hero has not reached other biomes");
                    Assert.That(request.Plant, Is.Empty, "mutations are not in the album yet");
                    Assert.That(request.Recipe, Is.Empty.Or.EqualTo("might").Or.EqualTo("fortune"), "only known recipes");
                    Assert.That(order.Amount, Is.InRange(request.Min, request.Max));
                }
            }
        }

        [Test]
        public void AnOrderTakesTheGoodsAndPaysReputationAndShards()
        {
            var loop = Town();
            var index = loop.State.Town.Orders.FindIndex(o => !Rules.Request(o.Request)!.WantsPotion);
            Assume.That(index, Is.GreaterThanOrEqualTo(0));
            var order = loop.State.Town.Orders[index];
            var request = Rules.Request(order.Request)!;
            Assert.That(loop.Town.Fulfil(index), Is.EqualTo(TownResult.NotEnough));
            Assert.That(TownView.Orders(loop.Town, loop.Camp, loop.Alchemy)[index].Ready, Is.False);
            loop.Camp.Add(request.Item, order.Amount + 1);
            Assert.That(TownView.Orders(loop.Town, loop.Camp, loop.Alchemy)[index].Ready, Is.True);
            var shards = loop.State.Drops.Shards;
            loop.Events.Clear();
            Assert.That(loop.Town.Fulfil(index), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Camp.Count(request.Item), Is.EqualTo(1));
            Assert.That(loop.State.Town.Orders.Count, Is.EqualTo(Rules.OrdersPerDay - 1));
            Assert.That(loop.Town.ReputationOf(request.Resident.Guild), Is.EqualTo(Rules.Reputation));
            Assert.That(loop.State.Drops.Shards - shards, Is.EqualTo(Rules.Shards));
            Assert.That(loop.Events.CountOf(GameEventKind.OrderDone), Is.EqualTo(1));
        }

        [Test]
        public void PotionOrdersTakeBrewedPotionsAndSometimesTeachARecipe()
        {
            var taught = 0;
            for (var seed = 1UL; seed <= 60; seed++)
            {
                var loop = Town(seed);
                loop.State.Town.Orders.Clear();
                loop.State.Town.Orders.Add(new TownOrder { Request = "orm_might", Amount = 2 });
                loop.State.Alchemy.Potions.Add(new Potion { Recipe = "might" });
                loop.State.Alchemy.Potions.Add(new Potion { Recipe = "might", Unknown = true });
                Assert.That(loop.Town.Fulfil(0), Is.EqualTo(TownResult.NotEnough), "an unknown potion does not count");
                loop.State.Alchemy.Potions.Add(new Potion { Recipe = "might", Fine = true });
                var known = loop.State.Alchemy.Known.Count;
                Assert.That(loop.Town.Fulfil(0), Is.EqualTo(TownResult.Done));
                Assert.That(loop.State.Alchemy.Potions.Single().Unknown, Is.True);
                taught += loop.State.Alchemy.Known.Count > known ? 1 : 0;
            }
            Assert.That(taught, Is.InRange(3, 25));
        }

        [Test]
        public void GiftsOnlyMoveHarvestWithinTheLimits()
        {
            var loop = Town();
            loop.Camp.Add("herb_windwort", 5);
            Assert.That(loop.Town.PackGift("gem_fireball", 1), Is.EqualTo(TownResult.NotGiftable));
            Assert.That(loop.Town.PackGift("herb_windwort", Rules.GiftMaxAmount + 1), Is.EqualTo(TownResult.BadAmount));
            Assert.That(loop.Town.PackGift("herb_windwort", 6), Is.EqualTo(TownResult.NotEnough));
            Assert.That(loop.Town.PackGift("herb_windwort", 3), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Camp.Count("herb_windwort"), Is.EqualTo(2));
            Assert.That(loop.Town.Receive("find_dragon_scale", 1), Is.EqualTo(TownResult.NotGiftable));
            Assert.That(loop.Town.Receive("herb_windwort", 4), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Camp.Count("herb_windwort"), Is.EqualTo(6));
        }

        [Test]
        public void TheBoardSurvivesTheSave()
        {
            var loop = Town();
            loop.State.Town.Reputation["gardeners"] = 30;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(JsonWriter.Write(GameStateJson.Write(loop.State))));
            Assert.That(copy.Town.Day, Is.EqualTo(Day));
        }

        [TestCase(@"""resident"": ""bryn"", ""item"": ""ruin_shard""", @"""resident"": ""nobody"", ""item"": ""ruin_shard""", "unknown resident 'nobody'")]
        [TestCase(@"""recipe"": ""vigor""", @"""recipe"": ""tea""", "unknown recipe 'tea'")]
        [TestCase(@"""biome"": ""ice_peak""", @"""biome"": ""moon""", "unknown biome 'moon'")]
        public void BrokenTownIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(TownRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => TownRules.Load(json, TestWorld.Data.Alchemy, TestWorld.Data.Garden, TestWorld.Data.Stages.Biomes))!;
            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
