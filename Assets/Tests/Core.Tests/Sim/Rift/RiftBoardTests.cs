using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftBoardTests
    {
        private static RiftRules Rules => RiftRules.Load(TestData.ReadGameDataFile(RiftRules.FileName));

        [Test]
        public void TheGameFileGivesAPartyRiftBoardWithARestTier()
        {
            var rules = Rules;

            Assert.That(rules.PartyJoinMs, Is.EqualTo(3_600_000));
            Assert.That(rules.PartyDurationMs, Is.EqualTo(24 * 3_600_000L));
            Assert.That(rules.Board.UpdateMinutes, Is.EqualTo(5));
            Assert.That(rules.Board.RewardFor(1).Shards, Is.GreaterThan(rules.Board.RewardFor(2).Shards));
            Assert.That(rules.Board.RewardFor(3).Shards, Is.EqualTo(rules.Board.RewardFor(2).Shards));
            Assert.That(rules.Board.Places[rules.Board.Places.Count - 1].Rest, Is.True);
            Assert.That(rules.Board.RewardFor(100000).IsEmpty, Is.False, "everyone who played gets something");
            Assert.That(rules.Board.RewardFor(1).Friendship, Is.GreaterThan(rules.Board.RewardFor(100000).Friendship));
            Assert.That(rules.Board.RewardFor(100000).Friendship, Is.Positive, "every place brings friendship tokens");
        }

        [Test]
        public void APlaceWithoutFriendshipGivesNone()
        {
            var json = TestData.ReadGameDataFile(RiftRules.FileName);
            const string from = @"""ingots"": 6, ""friendship"": 30 }";
            Assert.That(json, Does.Contain(from));

            var rules = RiftRules.Load(json.Replace(from, @"""ingots"": 6 }"));

            Assert.That(rules.Board.RewardFor(1).Friendship, Is.Zero);
            Assert.That(rules.Board.RewardFor(1).Ingots, Is.EqualTo(6));
        }

        [TestCase(@"{ ""shards"": 30, ""uncutGems"": 0, ""ingots"": 1,", @"{ ""upTo"": 99, ""shards"": 30, ""uncutGems"": 0, ""ingots"": 1,", "no upTo")]
        [TestCase(@"{ ""upTo"": 3, ""shards"": 200", @"{ ""upTo"": 1, ""shards"": 200", "places must grow")]
        [TestCase(@"{ ""upTo"": 3, ""shards"": 200", @"{ ""shards"": 200", "needs upTo")]
        public void BadPlaceTiersAreRejected(string from, string to, string message)
        {
            var json = TestData.ReadGameDataFile(RiftRules.FileName);
            Assert.That(json, Does.Contain(from));

            var error = Assert.Throws<ContentException>(() => RiftRules.Load(json.Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }

        [Test]
        public void ARewardGoesIntoTheDropWallet()
        {
            var state = new GameState();
            state.Drops.Shards = 5;

            RiftRewards.Apply(state, new RiftReward(200, 2, 4, 20));

            Assert.That(state.Drops.Shards, Is.EqualTo(205));
            Assert.That(state.Drops.UncutGems, Is.EqualTo(2));
            Assert.That(state.Drops.Ingots, Is.EqualTo(4));
            Assert.That(new RiftReward(0, 0, 0, 20).IsEmpty, Is.False, "friendship alone is a reward: the server already put it in the coop wallet");
        }

        [Test]
        public void TheBoardAndTheRewardReadInRussian()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");

            Assert.That(RiftBoardText.Row(2, "night_shift", 31, true), Does.StartWith("2. ").And.EndWith("· этап 31 · ваш отряд"));
            Assert.That(RiftBoardText.Own(2, 14, 31), Is.EqualTo("Ваш отряд: 2 место из 14, этап 31"));
        }
    }
}
