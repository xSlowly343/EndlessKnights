using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ShardTests
    {
        private static ShardRules Rules => TestWorld.Data.Drops.Shards;
        private static IEnumerable<GemDef> AllGems => TestWorld.Data.Gems.All;

        private static GameLoop Viking(StatSheet? hero = null, ulong seed = 7) =>
            TestWorld.Loop(state: TestWorld.Classless(new GameState(seed) { Class = HeroClass.Viking }), hero: hero);

        private static void Defeat(GameLoop loop, long stage, long recordBefore, EnemyType type = EnemyType.Boss, bool raisesRecord = true)
        {
            loop.Drops.OnBossDefeated(stage, type, recordBefore, raisesRecord);
            loop.Drops.Flush();
        }

        private static StatSheet With(StatId stat, ModifierKind kind, double value)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(stat, kind, value));
            return hero;
        }

        private static int Count(GameLoop loop, GameEventKind kind)
        {
            var count = 0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == kind)
                    count++;
            }
            return count;
        }

        [Test]
        public void FirstPassBossesGiveShardsByType()
        {
            var loop = Viking();

            Defeat(loop, 7, 7);
            Assert.That(loop.Drops.Shards, Is.EqualTo(Rules.Boss));

            Defeat(loop, 10, 10);
            Assert.That(loop.Drops.Shards, Is.EqualTo(Rules.Boss + Rules.SpecialBoss));

            Defeat(loop, 11, 11, EnemyType.Guardian);
            Assert.That(loop.Drops.Shards, Is.EqualTo(Rules.Boss + Rules.SpecialBoss + Rules.Guardian));
            Assert.That(loop.State.Drops.ShardsFound, Is.EqualTo(loop.Drops.Shards));
        }

        [Test]
        public void RepeatedBossesNearTheRecordGiveShardsUpToTheDailyLimit()
        {
            var loop = Viking();
            loop.SetLocalDay(1);

            for (var i = 0; i < 100; i++)
                Defeat(loop, 91, 100);

            Assert.That(loop.Drops.Shards, Is.EqualTo(Rules.DailyLimit));
            Assert.That(loop.Drops.DailyShardsLeft, Is.Zero);
        }

        [Test]
        public void BossesFarBelowTheRecordGiveNoShards()
        {
            var loop = Viking();

            Defeat(loop, 7, 100);

            Assert.That(loop.Drops.Shards, Is.Zero);
        }

        [Test]
        public void NewDayOpensTheDailyShardsAgain()
        {
            var loop = Viking();
            loop.SetLocalDay(1);
            for (var i = 0; i < 100; i++)
                Defeat(loop, 91, 100);

            loop.SetLocalDay(2);
            Defeat(loop, 91, 100);

            Assert.That(loop.Drops.Shards, Is.EqualTo(Rules.DailyLimit + Rules.Boss));
        }

        [Test]
        public void FirstPassShardsDoNotUseTheDailyLimit()
        {
            var loop = Viking();
            loop.SetLocalDay(1);

            for (var stage = 1; stage <= 50; stage++)
                Defeat(loop, stage, stage);

            Assert.That(loop.Drops.Shards, Is.GreaterThan(Rules.DailyLimit));
            Assert.That(loop.Drops.DailyShardsLeft, Is.EqualTo(Rules.DailyLimit));
        }

        [Test]
        public void NoHomeShardsInsideTheRift()
        {
            var loop = Viking();

            Defeat(loop, 7, 7, raisesRecord: false);

            Assert.That(loop.Drops.Shards, Is.Zero);
        }

        [Test]
        public void ShardDropStatRaisesShardsFromBosses()
        {
            var loop = Viking(With(CombatStats.ShardDrop, ModifierKind.Increased, 1));

            Defeat(loop, 10, 10);

            Assert.That(loop.Drops.Shards, Is.EqualTo(Rules.SpecialBoss * 2));
        }

        [Test]
        public void SmallShardBonusAddsUpOverManyBosses()
        {
            var loop = Viking(With(CombatStats.ShardDrop, ModifierKind.Increased, 0.1));

            for (var stage = 1; stage <= 100; stage++)
            {
                if (stage % 10 != 0)
                    Defeat(loop, stage, stage);
            }

            Assert.That(loop.Drops.Shards, Is.EqualTo(99).Within(1));
            Assert.That(loop.State.Drops.ShardCarry, Is.InRange(0, 1));
        }

        [Test]
        public void OfflineBossesGiveShardsFromTheSameDailyLimit()
        {
            var loop = Viking(seed: 41);
            loop.State.RecordStage = 1000;
            loop.SetLocalDay(1);
            loop.Events.Clear();

            loop.Drops.ApplyOffline(new OfflineResult(3600, 0, BigNumber.Zero, 700, 1000, 0, false, false, false));

            Assert.That(loop.Drops.Shards, Is.InRange(1, Rules.DailyLimit));
            Assert.That(loop.State.Drops.DailyShards, Is.EqualTo(loop.Drops.Shards));
            Assert.That(Count(loop, GameEventKind.ShardsGained), Is.GreaterThan(0));
        }

        [Test]
        public void AboutFivePercentOfDroppedGemsAreUncut()
        {
            var loop = Viking(seed: 47);
            const int bosses = 40000;

            for (var i = 0; i < bosses; i++)
                loop.Drops.OnKill(EnemyType.Boss);
            loop.Drops.Flush();

            var uncut = loop.Drops.UncutGems;
            var share = uncut / (double)(uncut + Count(loop, GameEventKind.GemDropped));
            Assert.That(share, Is.EqualTo(Rules.UncutShare).Within(0.01));
            Assert.That(Count(loop, GameEventKind.UncutGemDropped), Is.EqualTo(uncut));
            Assert.That(loop.State.Drops.UncutFound, Is.EqualTo(uncut));
        }

        [Test]
        public void UncutGemChanceStatAddsToTheShare()
        {
            var loop = Viking(With(CombatStats.UncutGemChance, ModifierKind.Flat, 0.15), seed: 47);
            const int bosses = 40000;

            for (var i = 0; i < bosses; i++)
                loop.Drops.OnKill(EnemyType.Boss);
            loop.Drops.Flush();

            var uncut = loop.Drops.UncutGems;
            var share = uncut / (double)(uncut + Count(loop, GameEventKind.GemDropped));
            Assert.That(share, Is.EqualTo(Rules.UncutShare + 0.15).Within(0.015));
        }

        [Test]
        public void ShardsBuyAnUncutGem()
        {
            var loop = Viking();
            loop.State.Drops.Shards = Rules.UncutCost + 1;

            Assert.That(loop.BuyUncutGem(), Is.True);
            Assert.That(loop.Drops.Shards, Is.EqualTo(1));
            Assert.That(loop.Drops.UncutGems, Is.EqualTo(1));
            Assert.That(loop.BuyUncutGem(), Is.False);
            Assert.That(loop.Drops.UncutGems, Is.EqualTo(1));
        }

        [Test]
        public void CuttingTurnsAnUncutGemIntoTheChosenOne()
        {
            var loop = Viking();
            var chosen = AllGems.First(gem => gem.Kind == GemKind.Support && DropSystem.Cuttable(gem));
            loop.State.Drops.UncutGems = 1;
            loop.Events.Clear();

            Assert.That(loop.CutGem(chosen), Is.EqualTo(CutResult.Cut));
            Assert.That(loop.State.OwnedGems, Does.Contain(chosen));
            Assert.That(loop.Drops.UncutGems, Is.Zero);
            Assert.That(Count(loop, GameEventKind.GemDropped), Is.EqualTo(1));
            Assert.That(loop.CutGem(chosen), Is.EqualTo(CutResult.NoUncut));
        }

        [Test]
        public void PathWeaponAndLockedGemsCannotBeCut()
        {
            var loop = Viking();
            loop.State.Drops.UncutGems = 3;
            var closed = new[]
            {
                AllGems.First(gem => gem.Ascendancy != null),
                AllGems.First(gem => gem.Kind == GemKind.Skill && gem.Skill?.Weapon != null),
                AllGems.First(gem => gem.Locked)
            };

            foreach (var gem in closed)
            {
                Assert.That(DropSystem.Cuttable(gem), Is.False, gem.Id);
                Assert.That(loop.CutGem(gem), Is.EqualTo(CutResult.NotCuttable), gem.Id);
            }
            Assert.That(loop.Drops.UncutGems, Is.EqualTo(3));
        }

        [Test]
        public void EveryCommonSkillSupportAndTriggerCanBeCut()
        {
            var loop = Viking();
            var cuttable = AllGems.Where(DropSystem.Cuttable).ToList();

            foreach (var kind in new[] { GemKind.Skill, GemKind.Support, GemKind.Trigger })
                Assert.That(loop.Drops.PoolOf(kind), Is.EquivalentTo(cuttable.Where(gem => gem.Kind == kind)), kind.ToString());
            Assert.That(cuttable.Any(gem => gem.Kind == GemKind.Support), Is.True);
        }

        [Test]
        public void ShardsAndUncutGemsSurviveSavingAndPrestige()
        {
            var loop = Viking();
            var drops = loop.State.Drops;
            drops.Shards = 42;
            drops.ShardCarry = 0.5;
            drops.ShardsFound = 50;
            drops.DailyShards = 3;
            drops.UncutGems = 2;
            drops.UncutFound = 4;
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State.Drops;

            Assert.That(loaded.Shards, Is.EqualTo(42));
            Assert.That(loaded.ShardCarry, Is.EqualTo(0.5));
            Assert.That(loaded.ShardsFound, Is.EqualTo(50));
            Assert.That(loaded.DailyShards, Is.EqualTo(3));
            Assert.That(loaded.UncutGems, Is.EqualTo(2));
            Assert.That(loaded.UncutFound, Is.EqualTo(4));

            loop.State.HighestStage = 500;
            Assert.That(loop.Prestige.TryPrestige(), Is.True);
            Assert.That(drops.Shards, Is.EqualTo(42));
            Assert.That(drops.UncutGems, Is.EqualTo(2));
        }
    }
}
