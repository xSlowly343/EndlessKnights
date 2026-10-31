using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SkillBranchTests
    {
        private const string Skill = "{ \"id\": \"forked_strike\", \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"tags\": [\"Keyboard\", \"Skill\"], " +
            "\"cost\": { \"resource\": 10 }, \"cooldownSeconds\": 4, \"effect\": { \"type\": \"strike\", \"multiplier\": 4 }, \"branches\": [" +
            "{ \"level\": 5, \"options\": [{ \"id\": \"heavy\", \"name\": { \"ru\": \"a\", \"en\": \"a\" }, \"damage\": 2 }, { \"id\": \"quick\", \"name\": { \"ru\": \"b\", \"en\": \"b\" }, \"cooldown\": 0.5 }] }, " +
            "{ \"level\": 15, \"options\": [{ \"id\": \"deep\", \"name\": { \"ru\": \"c\", \"en\": \"c\" }, \"effect\": { \"multiplier\": 8 } }, { \"id\": \"cheap\", \"name\": { \"ru\": \"d\", \"en\": \"d\" }, \"cost\": 0.5 }] }, " +
            "{ \"level\": 25, \"options\": [{ \"id\": \"wide\", \"name\": { \"ru\": \"e\", \"en\": \"e\" }, \"effect\": { \"queueTargets\": 2 } }, { \"id\": \"loud\", \"name\": { \"ru\": \"f\", \"en\": \"f\" }, \"damage\": 3 }] }] }";

        private static readonly GameData Data = GameData.Load(name =>
            name == GemCatalog.FileName ? Game.Core.Tests.Content.TestData.ReadGameDataFile(name).Replace("\"gems\": [", "\"gems\": [\n    " + Skill + ",") : Game.Core.Tests.Content.TestData.ReadGameDataFile(name));

        private static readonly GemDef Forked = Data.Gems["forked_strike"];

        private static (GameLoop Loop, SkillSlot Slot) Hero(int skillLevel)
        {
            var input = new FakeInputSource();
            var loop = new GameLoop(Data, input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = HeroClass.Ronin, HeroLevel = 30 }), new Game.Core.Stats.StatSheet());
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.ProgressOf(Forked).Level = skillLevel;
            var index = loop.Skills.Equip(Forked, role: SkillSlotRole.Left);
            return (loop, loop.State.Skills[index]);
        }

        private static StrikeEffect Strike(SkillSlot slot) => (StrikeEffect)slot.Def.Effect;

        [Test]
        public void ABranchWaitsForItsSkillLevel()
        {
            var (loop, _) = Hero(4);

            Assert.That(loop.ChooseBranch(Forked, 0, 0), Is.EqualTo(BranchResult.LevelTooLow));
            Assert.That(loop.ChooseBranch(Forked, 3, 0), Is.EqualTo(BranchResult.NoSuchBranch));
            Assert.That(loop.ChooseBranch(Forked, 0, 2), Is.EqualTo(BranchResult.NoSuchBranch));
        }

        [Test]
        public void TheFirstTierChangesDamageOrCooldown()
        {
            var (plainLoop, plain) = Hero(5);
            var (heavyLoop, heavy) = Hero(5);
            var (quickLoop, quick) = Hero(5);

            Assert.That(heavyLoop.ChooseBranch(Forked, 0, 0), Is.EqualTo(BranchResult.Done));
            Assert.That(quickLoop.ChooseBranch(Forked, 0, 1), Is.EqualTo(BranchResult.Done));
            foreach (var loop in new[] { plainLoop, heavyLoop, quickLoop })
            {
                loop.State.Resource = 100;
                Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
            }

            Assert.That(heavy.CastPower / plain.CastPower, Is.EqualTo(2).Within(1e-9));
            Assert.That(quick.CastPower / plain.CastPower, Is.EqualTo(1).Within(1e-9));
            Assert.That(quick.CooldownTotalTicks * 2, Is.EqualTo(plain.CooldownTotalTicks));
            Assert.That(heavy.CooldownTotalTicks, Is.EqualTo(plain.CooldownTotalTicks));
        }

        [Test]
        public void TheSecondTierChangesTheEffectOrTheCost()
        {
            var (deepLoop, deep) = Hero(15);
            var (cheapLoop, cheap) = Hero(15);

            deepLoop.ChooseBranch(Forked, 1, 0);
            cheapLoop.ChooseBranch(Forked, 1, 1);

            Assert.That(Strike(deep).Multiplier, Is.EqualTo(8));
            Assert.That(deep.Def.ResourceCost, Is.EqualTo(10));
            Assert.That(Strike(cheap).Multiplier, Is.EqualTo(4));
            Assert.That(cheap.Def.ResourceCost, Is.EqualTo(5));
        }

        [Test]
        public void TheThirdTierStacksOnTheEarlierOnes()
        {
            var (loop, slot) = Hero(25);

            loop.ChooseBranch(Forked, 0, 1);
            loop.ChooseBranch(Forked, 1, 0);
            loop.ChooseBranch(Forked, 2, 0);

            Assert.That(Strike(slot).Multiplier, Is.EqualTo(8));
            Assert.That(Strike(slot).QueueTargets, Is.EqualTo(2));
            Assert.That(slot.Def.CooldownSeconds, Is.EqualTo(2));
            Assert.That(loop.ChooseBranch(Forked, 2, 1), Is.EqualTo(BranchResult.Done));
            Assert.That(Strike(slot).QueueTargets, Is.EqualTo(0));
            Assert.That(slot.BranchDamage, Is.EqualTo(3));
        }

        [Test]
        public void ARebirthClosesTheTiersUntilTheLevelReturns()
        {
            var (loop, slot) = Hero(25);
            loop.ChooseBranch(Forked, 1, 0);
            loop.State.ProgressOf(Forked).Experience = 0;

            Assert.That(loop.Book.Rebirth(Forked, 25), Is.True);
            Assert.That(Strike(slot).Multiplier, Is.EqualTo(4));
            Assert.That(loop.State.ProgressOf(Forked).Branches[1], Is.EqualTo(0));

            loop.GemExperience.Grant(Forked, 1e12);
            Assert.That(loop.State.ProgressOf(Forked).Level, Is.EqualTo(25));
            Assert.That(Strike(slot).Multiplier, Is.EqualTo(8));
        }

        [Test]
        public void ChangingABranchIsFreeUntilTheFirstGateAndCostsGoldAfter()
        {
            var (loop, _) = Hero(5);
            loop.ChooseBranch(Forked, 0, 0);
            Assert.That(loop.BranchChangeCost(Forked, 0), Is.EqualTo(BigNumber.Zero));

            loop.State.GatesCleared.Add(500);
            var cost = loop.BranchChangeCost(Forked, 0);
            Assert.That(cost > BigNumber.Zero, Is.True);
            loop.State.Gold = BigNumber.Zero;
            Assert.That(loop.ChooseBranch(Forked, 0, 1), Is.EqualTo(BranchResult.NotEnoughGold));

            loop.State.Gold = cost;
            Assert.That(loop.ChooseBranch(Forked, 0, 1), Is.EqualTo(BranchResult.Done));
            Assert.That(loop.State.Gold, Is.EqualTo(BigNumber.Zero));
        }

        [Test]
        public void AFirstChoiceIsAlwaysFree()
        {
            var (loop, _) = Hero(15);
            loop.State.GatesCleared.Add(500);

            Assert.That(loop.BranchChangeCost(Forked, 1), Is.EqualTo(BigNumber.Zero));
        }

        [Test]
        public void CastsGrowTheSkill()
        {
            var (loop, _) = Hero(1);
            loop.State.Resource = 100;
            var before = loop.State.ProgressOf(Forked).Experience;

            loop.RequestCast(0);
            loop.Tick();

            Assert.That(loop.State.ProgressOf(Forked).Experience - before, Is.GreaterThanOrEqualTo(TestWorld.Data.Gems.Levels.ExperiencePerCast));
        }

        [Test]
        public void TheSaveKeepsTheChosenBranches()
        {
            var (loop, _) = Hero(25);
            loop.ChooseBranch(Forked, 0, 1);
            loop.ChooseBranch(Forked, 2, 0);
            loop.State.ProgressOf(Data.Gems["through_stride"]).Branches[0] = 1;

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12))), Data).State;
            var again = new GameLoop(Data, new FakeInputSource(), loaded, new Game.Core.Stats.StatSheet());

            Assert.That(loaded.ProgressOf(Forked).Branches, Is.EqualTo(new[] { 1, -1, 0 }));
            Assert.That(loaded.ProgressOf(Data.Gems["through_stride"]).Branches, Is.EqualTo(new[] { -1, -1, -1 }), "a skill without branches keeps none");
            Assert.That(Strike(again.State.Skills[0]).QueueTargets, Is.EqualTo(2));
            Assert.That(again.State.Skills[0].Def.CooldownSeconds, Is.EqualTo(2));
        }
    }
}
