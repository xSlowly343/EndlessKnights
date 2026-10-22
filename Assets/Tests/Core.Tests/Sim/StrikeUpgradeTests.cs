using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StrikeUpgradeTests
    {
        private static StrikeUpgradeRules Rules => TestWorld.Data.Combat.StrikeUpgrade;

        private static GameLoop WithGold(double gold) => TestWorld.Loop(state: TestWorld.Classless(new GameState { Gold = BigNumber.FromDouble(gold) }));

        [Test]
        public void DamageGrowsPerLevelAndDoublesAtMilestones()
        {
            Assert.That(Rules.DamageMultiplier(0), Is.EqualTo(1));
            Assert.That(Rules.DamageMultiplier(10), Is.EqualTo(1 + 10 * Rules.DamagePerLevel).Within(1e-12));
            Assert.That(Rules.DamageMultiplier(Rules.MilestoneEvery), Is.EqualTo((1 + Rules.MilestoneEvery * Rules.DamagePerLevel) * Rules.MilestoneMultiplier).Within(1e-12));
        }

        [Test]
        public void CostOfSeveralLevelsIsTheSumOfEachLevel()
        {
            var loop = WithGold(0);
            var expected = Rules.Cost.At(1) + Rules.Cost.At(2) + Rules.Cost.At(3);

            Assert.That(loop.Upgrades.CostFor(1), Is.EqualTo(Rules.Cost.At(1)));
            Assert.That(loop.Upgrades.CostFor(3).ToDouble(), Is.EqualTo(expected.ToDouble()).Within(1e-9 * expected.ToDouble()));
        }

        [Test]
        public void BuyingSpendsGoldAndMakesTheHeroStronger()
        {
            var loop = WithGold(1000);
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            var cost = loop.Upgrades.CostFor(5);

            Assert.That(loop.Upgrades.TryBuy(5), Is.True);

            Assert.That(loop.Upgrades.Level, Is.EqualTo(5));
            Assert.That(loop.State.Gold.ToDouble(), Is.EqualTo(1000 - cost.ToDouble()).Within(1e-6));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before * Rules.DamageMultiplier(5)).Within(1e-9));
        }

        [Test]
        public void NotEnoughGoldBuysNothing()
        {
            var loop = WithGold(1);

            Assert.That(loop.Upgrades.TryBuy(1), Is.False);
            Assert.That(loop.Upgrades.Affordable(), Is.EqualTo(0));
            Assert.That(loop.Upgrades.BuyMax(), Is.EqualTo(0));
            Assert.That(loop.Upgrades.Level, Is.EqualTo(0));
        }

        [TestCase(10.0)]
        [TestCase(12345.0)]
        [TestCase(1e30)]
        [TestCase(1e300)]
        public void AffordableIsTheMostLevelsTheGoldCovers(double gold)
        {
            var loop = WithGold(gold);

            var levels = loop.Upgrades.Affordable();

            Assert.That(loop.Upgrades.CostFor(levels) <= loop.State.Gold, Is.True);
            Assert.That(loop.Upgrades.CostFor(levels + 1) > loop.State.Gold, Is.True);
        }

        [Test]
        public void BuyMaxSpendsDownToLessThanTheNextLevel()
        {
            var loop = WithGold(1e6);

            var bought = loop.Upgrades.BuyMax();

            Assert.That(bought, Is.GreaterThan(0));
            Assert.That(loop.State.Gold < loop.Upgrades.CostFor(1), Is.True);
        }

        [Test]
        public void PrestigeBurnsTheUpgrades()
        {
            var state = TestWorld.Classless(new GameState { Gold = BigNumber.FromDouble(1e6), HighestStage = TestWorld.Data.Prestige.MinStage, Stage = TestWorld.Data.Prestige.MinStage, RunGoldEarned = BigNumber.FromDouble(1e9) });
            var loop = TestWorld.Loop(state: state);
            var plain = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            loop.Upgrades.BuyMax();

            Assert.That(loop.Prestige.TryPrestige(), Is.True);

            Assert.That(loop.Upgrades.Level, Is.EqualTo(0));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void UpgradeLevelSurvivesSaveAndLoad()
        {
            var loop = WithGold(1e6);
            loop.Upgrades.BuyMax();
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            var again = TestWorld.Loop(state: loaded);

            Assert.That(loaded.StrikeLevel, Is.EqualTo(loop.Upgrades.Level));
            Assert.That(again.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard)).Within(1e-9));
        }

        [Test]
        public void OlderSaveStartsWithoutUpgrades()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v6.json"), TestWorld.Data);

            Assert.That(save.State.StrikeLevel, Is.EqualTo(0));
        }

        [Test]
        public void BrokenMilestoneIsReported()
        {
            var json = TestData.ReadGameDataFile(CombatRules.FileName).Replace("\"every\": 25", "\"every\": 0");

            var error = Assert.Throws<ContentException>(() => CombatRules.Load(json))!;

            Assert.That(error.Message, Does.Contain("strikeUpgrade.milestone.every"));
        }
    }
}
