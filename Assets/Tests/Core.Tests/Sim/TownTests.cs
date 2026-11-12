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
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(Rules.Dust), "orders are a second source of dust");
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
        public void TheMarketSellsOnlyCompanionTreatsThatAreNotDelicacies()
        {
            var treats = TestWorld.Data.Companions.Treats;
            Assert.That(Rules.Market.Items, Is.Not.Empty);
            Assert.That(Rules.Market.Items.All(item => treats.Treat(item) is { Delicacy: false }), Is.True);
            Assert.That(treats.Treats.Where(t => !t.Delicacy).All(t => Rules.Market.Sells(t.Item)), Is.True, "every plain treat can be sold");
            Assert.That(Rules.Market.Currency, Is.Not.EqualTo("crystals"));
        }

        [Test]
        public void ALotTakesTheTreatsAndGivesThemBackWhenTheServerRefuses()
        {
            var loop = Town();
            var market = Rules.Market;
            var treat = market.Items[0];
            loop.Camp.Add(treat, 6);
            loop.Camp.Add("herb_windwort", 5);

            Assert.That(loop.Town.PackLot("herb_windwort", 1, market.MinPrice, 0), Is.EqualTo(TownResult.NotForSale));
            Assert.That(loop.Town.PackLot(treat, 0, market.MinPrice, 0), Is.EqualTo(TownResult.BadAmount));
            Assert.That(loop.Town.PackLot(treat, market.MaxAmount + 1, market.MinPrice, 0), Is.EqualTo(TownResult.BadAmount));
            Assert.That(loop.Town.PackLot(treat, 1, market.MaxPrice + 1, 0), Is.EqualTo(TownResult.BadPrice));
            Assert.That(loop.Town.PackLot(treat, 1, market.MinPrice, market.MaxLots), Is.EqualTo(TownResult.TooManyLots));
            Assert.That(loop.Town.PackLot(treat, 7, market.MinPrice, 0), Is.EqualTo(TownResult.NotEnough));
            Assert.That(loop.Camp.Count(treat), Is.EqualTo(6));

            Assert.That(loop.Town.PackLot(treat, 4, market.MinPrice, 0), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Town.PackLot(treat, 2, market.MaxPrice, 0), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Camp.Count(treat), Is.Zero);
            var first = loop.State.Town.Offers[0];
            var second = loop.State.Town.Offers[1];
            Assert.That(first.Nonce, Is.Not.EqualTo(second.Nonce));
            Assert.That(first.Nonce, Does.Match("^[a-z0-9]{1,32}$"));

            Assert.That(loop.Town.SettleLot(first.Nonce), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Town.ReturnLot(first.Nonce), Is.EqualTo(TownResult.NoOffer), "a listed lot is not returned twice");
            Assert.That(loop.Town.ReturnLot(second.Nonce), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Camp.Count(treat), Is.EqualTo(2));
            Assert.That(loop.State.Town.Offers, Is.Empty);
        }

        [Test]
        public void ABoughtLotArrivesOnceAndUnsentLotsSurviveTheSave()
        {
            var loop = Town();
            var treat = Rules.Market.Items[0];
            loop.Camp.Add(treat, 3);
            loop.Town.PackLot(treat, 3, Rules.Market.MinPrice, 0);

            Assert.That(loop.Town.ReceiveLot("ann_d1n1_17", treat, 5), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Town.ReceiveLot("ann_d1n1_17", treat, 5), Is.EqualTo(TownResult.Already));
            Assert.That(loop.Town.ReceiveLot("ann_d1n2_18", "gem_fireball", 1), Is.EqualTo(TownResult.NotForSale));
            Assert.That(loop.Town.ReceiveLot("ann_d1n3_19", treat, Rules.Market.MaxAmount + 1), Is.EqualTo(TownResult.BadAmount));
            Assert.That(loop.Camp.Count(treat), Is.EqualTo(5));

            var loaded = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(loaded.Town.Offers.Single().Nonce, Is.EqualTo(loop.State.Town.Offers[0].Nonce));
            Assert.That(loaded.Town.Offers.Single().Amount, Is.EqualTo(3));
            Assert.That(loaded.Town.Deliveries, Is.EqualTo(new[] { "ann_d1n1_17" }));
            Assert.That(loaded.Town.OfferSeq, Is.EqualTo(1));
        }

        [Test]
        public void TheMarketSpeaksRussianAndCountsTheFee()
        {
            var treats = TestWorld.Data.Companions.Treats;
            var market = Rules.Market;

            Assert.That(market.Fees(20), Is.EqualTo((long)System.Math.Ceiling(20 * market.Fee)));
            Assert.That(market.Fees(1), Is.Zero, "the seller of the cheapest lot still gets a token");
            Assert.That(market.Proceeds(5, 4), Is.EqualTo(20 - market.Fees(20)));
            Assert.That(MarketText.Lot(treats, "treat_dried_crucian", 5, 4, "Ann"), Is.EqualTo("Сушёный карась ×5 · 4 за штуку, всего 20 · Ann"));
            Assert.That(MarketText.Own(treats, "treat_dried_crucian", 2, 3, true), Does.Contain("срок вышел"));
            Assert.That(MarketText.Result(TownResult.TooManyLots), Is.EqualTo("Все твои места на прилавке заняты"));
            Assert.That(MarketText.Refusal("not enough friendship"), Is.EqualTo("Не хватает Знаков дружбы"));
            Assert.That(MarketText.Refusal("lot is gone"), Is.EqualTo("Этот лот уже купили или сняли"));
            Assert.That(MarketText.Rejected("bad price"), Is.True);
            Assert.That(MarketText.Rejected("timeout"), Is.False, "a lost answer keeps the lot waiting for a retry");
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
        public void AGiftIsTakenOnceEvenIfTheServerSendsItAgain()
        {
            var loop = Town();
            Assert.That(loop.Town.ReceiveGift("ann_1", "herb_windwort", 4), Is.EqualTo(TownResult.Done));
            Assert.That(loop.Town.ReceiveGift("ann_1", "herb_windwort", 4), Is.EqualTo(TownResult.Already));
            Assert.That(loop.Town.ReceiveGift("ann_2", "find_dragon_scale", 1), Is.EqualTo(TownResult.NotGiftable));
            Assert.That(loop.Camp.Count("herb_windwort"), Is.EqualTo(4));
            for (var i = 0; i < Rules.GiftInbox * 3; i++)
                loop.Town.ReceiveGift("bob_" + i, "herb_windwort", 1);
            Assert.That(loop.State.Town.Gifts.Count, Is.EqualTo(Rules.GiftInbox * 2));

            var copy = Game.Core.Save.GameStateJson.Read(new Game.Core.Content.ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Town.Gifts, Is.EqualTo(loop.State.Town.Gifts));
        }

        [Test]
        public void OldOrdersLeaveTheBoardAndNewOnesCome()
        {
            var loop = Town();
            loop.State.Town.Orders.Clear();
            loop.State.Town.Day = 99;
            loop.Town.SetDay(100);
            var first = loop.State.Town.Orders.Select(order => order.Request).ToList();
            Assert.That(first, Is.Not.Empty);
            Assert.That(loop.State.Town.Orders.All(order => order.Day == 100), Is.True);

            loop.Town.SetDay(100 + Rules.OrderDays - 1);
            Assert.That(loop.State.Town.Orders.Count(order => order.Day == 100), Is.EqualTo(first.Count));
            loop.Town.SetDay(100 + Rules.OrderDays);
            Assert.That(loop.State.Town.Orders.Any(order => order.Day == 100), Is.False);
            Assert.That(loop.State.Town.Orders, Is.Not.Empty);
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
