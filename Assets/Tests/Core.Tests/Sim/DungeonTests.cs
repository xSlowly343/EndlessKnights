using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DungeonTests
    {
        private static DungeonRules Rules => TestWorld.Data.Dungeons;

        private static (GameLoop Loop, FakeInputSource Input) Hero(long stage = 300, double power = 1e12)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, power));
            var state = new GameState(6) { Class = HeroClass.Runedancer, Stage = stage, HighestStage = stage, RecordStage = stage };
            return (TestWorld.Loop(input, state, hero), input);
        }

        private static DungeonKey Key(int tier, params string[] modifiers) => new DungeonKey(tier, modifiers, 0, Rules.KeyAttempts);

        private static DungeonKey KeyAt(int tier, long stage) => new DungeonKey(tier, System.Array.Empty<string>(), stage, Rules.KeyAttempts);

        [Test]
        public void SixteenTiersAndTheLateOnesWaitForTheLimit()
        {
            Assert.That(Rules.Tiers.Count, Is.EqualTo(16));
            Assert.That(Rules.Tiers.Count(t => t.NeedsLimit), Is.EqualTo(7));
            Assert.That(Rules.HighestTierFor(149, false), Is.EqualTo(0));
            Assert.That(Rules.HighestTierFor(160, false), Is.EqualTo(1));
            Assert.That(Rules.HighestTierFor(60000, false), Is.EqualTo(9));
            Assert.That(Rules.HighestTierFor(60000, true), Is.EqualTo(13));
            Assert.That(Rules.Tiers.Where(t => t.NeedsLimit).All(t => t.UniqueChance > 0), Is.True);
        }

        [Test]
        public void KeysDropFromBossesFromStage150()
        {
            var (loop, _) = Hero();
            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(100, EnemyType.Guardian, 100, raisesRecord: true);
                loop.Drops.Flush();
            }
            Assert.That(loop.Dungeons.Keys, Is.Empty);

            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(300, EnemyType.Guardian, 300, raisesRecord: true);
                loop.Drops.Flush();
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(loop.Dungeons.Keys.Count, Is.InRange(8, 32));
            Assert.That(loop.Dungeons.Keys.All(k => k.Tier >= 1 && k.Tier <= 3), Is.True);
            Assert.That(loop.Dungeons.Keys.All(k => k.Modifiers.Count >= 1 && k.Modifiers.Count <= Rules.MaxModifiers), Is.True);
            Assert.That(loop.Dungeons.Keys.All(k => k.Modifiers.Distinct().Count() == k.Modifiers.Count), Is.True);
            Assert.That(loop.Dungeons.Keys.All(k => k.Stage == 300 && k.Attempts == Rules.KeyAttempts), Is.True);
        }

        [Test]
        public void TheBotTurnsAutoRunOnAndRetriesAFailedKeyOnlyWhenWellPastIt()
        {
            var (loop, _) = Hero(300);
            var tooDeep = KeyAt(2, 290);
            tooDeep.Attempts--;
            var outgrown = KeyAt(1, 200);
            outgrown.Attempts--;
            loop.State.Dungeons.Keys.Add(tooDeep);
            loop.State.Dungeons.Keys.Add(outgrown);
            var bot = new Game.Core.Sim.Headless.PlayerBot(TestWorld.Data, loop, 30, gear: true);

            bot.EnterDungeon();

            Assert.That(loop.State.Dungeons.Auto, Is.True);
            Assert.That(loop.State.Dungeons.Active, Is.SameAs(outgrown));
            Assert.That(loop.Dungeons.Keys, Is.EquivalentTo(new[] { tooDeep }));
            bot.EnterDungeon();
            Assert.That(loop.State.Dungeons.Active, Is.SameAs(outgrown), "no second dungeon from inside");
        }

        [Test]
        public void ADungeonStandsOnTheStageWhereTheKeyDroppedAndLootIsOfThatStage()
        {
            var (loop, input) = Hero(5000, 1e60);
            var key = KeyAt(4, 4321);
            loop.State.Dungeons.Keys.Add(key);
            loop.Events.Clear();

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
            Assert.That(loop.State.Stage, Is.EqualTo(4322));

            var levels = new System.Collections.Generic.List<long>();
            for (var i = 0; i < 6000 && loop.Dungeons.Inside; i++)
            {
                input.PressKeys(3);
                loop.Tick();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Drop?.Source == DropSource.Dungeon)
                        levels.Add(loop.Events[e].Stage);
                }
                loop.Events.Clear();
            }

            Assert.That(loop.Dungeons.Inside, Is.False);
            Assert.That(levels, Is.Not.Empty);
            Assert.That(levels.All(l => l == 4321), Is.True);
        }

        [Test]
        public void ADungeonNeverStartsOnAGuardianStage()
        {
            var (loop, _) = Hero(5000);

            Assert.That(loop.Dungeons.StartStage(KeyAt(4, 1248)), Is.EqualTo(1251));
            Assert.That(loop.Dungeons.StartStage(KeyAt(4, 1249)), Is.EqualTo(1251));
            Assert.That(loop.Dungeons.StartStage(KeyAt(4, 1250)), Is.EqualTo(1251));
            Assert.That(loop.Dungeons.StartStage(KeyAt(4, 1300)), Is.EqualTo(1301));
        }

        [Test]
        public void DungeonEnemiesAreThickerThanTheSameStageAtHome()
        {
            var (loop, _) = Hero(5000);
            var home = TestWorld.Data.Stages.EnemyHp(4322);
            var key = KeyAt(4, 4321);
            loop.State.Dungeons.Keys.Add(key);

            loop.Dungeons.Enter(key);

            Assert.That(Rules.DepthEnemyHp, Is.GreaterThan(1));
            Assert.That(loop.State.Stage, Is.EqualTo(4322));
            Assert.That((TestWorld.Data.Stages.ForDepth(Rules.DepthEnemyHp, 1, 45).EnemyHp(4322) / home).ToDouble(), Is.EqualTo(Rules.DepthEnemyHp).Within(1e-6));
            Assert.That(loop.State.EnemyMaxHp.Log10(), Is.GreaterThan(home.Log10() - 1));
        }

        [Test]
        public void AFailedKeyComesBackUntilItsAttemptsRunOut()
        {
            var (loop, _) = Hero();
            var key = Key(1);
            loop.State.Dungeons.Keys.Add(key);
            Assume.That(Rules.KeyAttempts, Is.EqualTo(3));

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
                Assert.That(loop.Dungeons.Abandon(), Is.True);
                Assert.That(loop.State.Dungeons.Failed, Is.EqualTo(attempt));
                Assert.That(loop.Dungeons.Keys.Contains(key), Is.EqualTo(attempt < 3));
            }

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.NoKey));
        }

        [Test]
        public void TheKeyBagHasNoLimit()
        {
            var (loop, _) = Hero();
            for (var i = 0; i < 500; i++)
                loop.State.Dungeons.Keys.Add(Key(1));

            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(300, EnemyType.Guardian, 300, raisesRecord: true);
                loop.Drops.Flush();
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(loop.Dungeons.Keys.Count, Is.GreaterThan(505));
            Assert.That(loop.State.Dungeons.Salvaged, Is.Zero);
        }

        [Test]
        public void AutoMergeFoldsKeysOfATierOnlyWhenTheBoxIsTicked()
        {
            var (loop, _) = Hero(5000);
            Assume.That(Rules.MergeCount, Is.EqualTo(3));
            for (var i = 0; i < 9; i++)
                loop.State.Dungeons.Keys.Add(KeyAt(1, 200 + i));
            loop.State.Dungeons.Keys.Add(KeyAt(1, 150));

            for (var i = 0; i < 30; i++)
                loop.Tick();
            Assert.That(loop.Dungeons.Keys.Count, Is.EqualTo(10), "nothing merges by itself");

            loop.Dungeons.SetAutoMerge(true);

            Assert.That(loop.Dungeons.Keys.Select(k => k.Tier).OrderBy(t => t), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(loop.Dungeons.Keys.Single(k => k.Tier == 3).Stage, Is.EqualTo(208));
            Assert.That(loop.State.Dungeons.Merged, Is.EqualTo(4));

            loop.State.Dungeons.Keys.Add(KeyAt(1, 300));
            loop.State.Dungeons.Keys.Add(KeyAt(1, 301));
            for (var i = 0; i < 30; i++)
                loop.Tick();
            Assert.That(loop.Dungeons.Keys.Select(k => k.Tier).OrderBy(t => t), Is.EqualTo(new[] { 2, 3 }), "new keys fold on the next check");
        }

        [Test]
        public void AutoRunSkipsKeysWithAModifierThePlayerRuledOut()
        {
            var (loop, _) = Hero(5000, 1e60);
            var banned = new DungeonKey(5, new[] { "no_focus" }, 700, Rules.KeyAttempts);
            var fine = new DungeonKey(2, new[] { "hurry" }, 400, Rules.KeyAttempts);
            loop.State.Dungeons.Keys.Add(banned);
            loop.State.Dungeons.Keys.Add(fine);
            loop.Dungeons.SetAutoSkip("no_focus", true);
            loop.Dungeons.SetAutoSkip("no_such_modifier", true);

            Assert.That(loop.State.Dungeons.AutoSkip, Is.EqualTo(new[] { "no_focus" }));
            Assert.That(loop.Dungeons.AutoKey(), Is.SameAs(fine));
            loop.Dungeons.SetAutoSkip("no_focus", false);
            Assert.That(loop.Dungeons.AutoKey(), Is.SameAs(banned));
        }

        [Test]
        public void TheKeyFilterNarrowsAndSortsTheList()
        {
            var (loop, _) = Hero(5000);
            var low = KeyAt(1, 200);
            var deep = new DungeonKey(3, new[] { "hurry" }, 900, Rules.KeyAttempts);
            var shallow = new DungeonKey(3, new[] { "no_focus", "hurry" }, 700, Rules.KeyAttempts - 1);
            var rift = new DungeonKey(2, new[] { "hurry" }, 40, Rules.KeyAttempts, 77);
            loop.State.Dungeons.Keys.AddRange(new[] { low, shallow, rift, deep });
            var shown = new System.Collections.Generic.List<DungeonKey>();

            new DungeonKeyFilter().Collect(loop.Dungeons, shown);
            Assert.That(shown, Is.EqualTo(new[] { deep, shallow, low, rift }));

            new DungeonKeyFilter { MinTier = 2, Kind = DungeonKeyKind.Home }.Collect(loop.Dungeons, shown);
            Assert.That(shown, Is.EqualTo(new[] { deep, shallow }));

            new DungeonKeyFilter { FreshOnly = true, WithModifier = "hurry" }.Collect(loop.Dungeons, shown);
            Assert.That(shown, Is.EqualTo(new[] { deep, rift }));

            new DungeonKeyFilter { WithoutModifier = "no_focus", MaxTier = 2 }.Collect(loop.Dungeons, shown);
            Assert.That(shown, Is.EqualTo(new[] { low, rift }));

            new DungeonKeyFilter { Kind = DungeonKeyKind.Rift }.Collect(loop.Dungeons, shown);
            Assert.That(shown, Is.EqualTo(new[] { rift }));
        }

        [Test]
        public void InASharedFightTheKeyChanceIsSplitBetweenTheParty()
        {
            Assert.That(Rules.PartyDivisor(0), Is.EqualTo(1));
            Assert.That(Rules.PartyDivisor(3), Is.EqualTo(1 + 3 * Rules.PartySplit));
            var (solo, _) = Hero();
            var (party, _) = Hero();
            party.State.PartyAllies = 3;

            foreach (var loop in new[] { solo, party })
            {
                for (var i = 0; i < 400; i++)
                {
                    loop.Drops.OnBossDefeated(300, EnemyType.Guardian, 300, raisesRecord: true);
                    loop.Drops.Flush();
                    loop.State.Weapons.Bag.Clear();
                    loop.State.Drops.Gear.Clear();
                }
            }

            Assert.That(solo.Dungeons.Keys.Count, Is.InRange(160, 240));
            Assert.That(party.Dungeons.Keys.Count, Is.InRange(55, 110));
        }

        private static (GameLoop Loop, FakeInputSource Input) InRift(double power = 1e12)
        {
            var (loop, input) = Hero(300, power);
            var window = loop.Rift.Rules.Schedule.Next(0);
            Assert.That(loop.Rift.Enter(window.StartUnixMs + 1), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            return (loop, input);
        }

        [Test]
        public void RiftBossesDropRiftKeysAndHomeBossesDoNot()
        {
            var (loop, _) = InRift();
            var window = loop.State.Rift.WindowId;

            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(60, EnemyType.Guardian, 300, raisesRecord: false);
                loop.Drops.Flush();
            }

            Assert.That(loop.Dungeons.Keys.Count, Is.InRange(8, 32));
            Assert.That(loop.Dungeons.Keys.All(k => k.ForRift && k.Rift == window && k.Stage == 60), Is.True);
            Assert.That(loop.Dungeons.Keys.All(k => k.Tier == Rules.Rift.TierAt(60)), Is.True);

            var before = loop.Dungeons.Keys.Count;
            loop.Drops.OnBossDefeated(Rules.Rift.FromStage - 1, EnemyType.Guardian, 300, raisesRecord: false);
            loop.Drops.Flush();
            Assert.That(loop.Dungeons.Keys.Count, Is.EqualTo(before), "too shallow for a key");
        }

        [Test]
        public void ARiftKeyOpensADungeonOnlyInsideItsRiftAndPaysRiftShards()
        {
            var (loop, input) = InRift(1e30);
            var window = loop.State.Rift.WindowId;
            var key = new DungeonKey(2, new[] { "thick_hide" }, 40, Rules.KeyAttempts, window);
            var stale = new DungeonKey(2, System.Array.Empty<string>(), 40, Rules.KeyAttempts, window + 1);
            var homeKey = Key(1);
            loop.State.Dungeons.Keys.AddRange(new[] { key, stale, homeKey });
            loop.State.Stage = 30;
            var riftShards = loop.State.Rift.Shards;

            Assert.That(loop.Dungeons.Enter(homeKey), Is.EqualTo(DungeonResult.InsideRift));
            Assert.That(loop.Dungeons.Enter(stale), Is.EqualTo(DungeonResult.WrongKey));
            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
            Assert.That(loop.State.Stage, Is.EqualTo(41));

            for (var i = 0; i < 6000 && loop.Dungeons.Inside; i++)
            {
                input.PressKeys(3);
                loop.Tick();
                loop.Events.Clear();
            }

            Assert.That(loop.State.Dungeons.Completed, Is.EqualTo(1));
            Assert.That(loop.State.Rift.Inside, Is.True);
            Assert.That(loop.State.Stage, Is.EqualTo(30));
            Assert.That(loop.Stages.RaisesRecord, Is.False);
            Assert.That((loop.State.Rift.Shards - riftShards).ToDouble(), Is.GreaterThanOrEqualTo(Rules.Rift.Shards(2) * 1.3 - 1e-6));
            Assert.That(loop.State.Dungeons.BestTier, Is.Zero);

            loop.Rift.Leave();
            Assert.That(loop.Dungeons.Enter(stale), Is.Not.EqualTo(DungeonResult.Entered));
            var late = new DungeonKey(2, System.Array.Empty<string>(), 40, Rules.KeyAttempts, window);
            loop.State.Dungeons.Keys.Add(late);
            Assert.That(loop.Dungeons.Enter(late), Is.EqualTo(DungeonResult.OutsideRift));
        }

        [Test]
        public void LeavingTheRiftFromItsDungeonKeepsTheKeyAndBothWorlds()
        {
            var (loop, _) = InRift();
            var key = new DungeonKey(1, System.Array.Empty<string>(), 20, Rules.KeyAttempts, loop.State.Rift.WindowId);
            loop.State.Dungeons.Keys.Add(key);
            loop.State.Stage = 15;
            loop.Dungeons.Enter(key);

            Assert.That(loop.Rift.Leave(), Is.True);

            Assert.That(loop.Dungeons.Inside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(300));
            Assert.That(loop.Stages.RaisesRecord, Is.True);
            Assert.That(loop.State.Rift.OtherWorld!.Stage, Is.EqualTo(15));
            Assert.That(loop.Dungeons.Keys, Does.Contain(key));
            Assert.That(key.Attempts, Is.EqualTo(Rules.KeyAttempts));
        }

        [Test]
        public void KeysOfAClosedRiftDisappearAndRiftKeysMergeOnlyWithEachOther()
        {
            var (loop, _) = InRift();
            var window = loop.State.Rift.WindowId;
            for (var i = 0; i < 3; i++)
                loop.State.Dungeons.Keys.Add(new DungeonKey(1, System.Array.Empty<string>(), 20 + i, Rules.KeyAttempts, window));
            loop.State.Dungeons.Keys.Add(KeyAt(1, 200));
            loop.State.Dungeons.Keys.Add(KeyAt(1, 201));

            Assert.That(loop.Dungeons.Merge(1), Is.EqualTo(DungeonKeyResult.NotEnoughKeys));
            Assert.That(loop.Dungeons.Merge(1, rift: true), Is.EqualTo(DungeonKeyResult.Done));
            var merged = loop.Dungeons.Keys.Single(k => k.ForRift);
            Assert.That(merged.Tier, Is.EqualTo(2));
            Assert.That(merged.Rift, Is.EqualTo(window));
            Assert.That(merged.Stage, Is.EqualTo(22));

            loop.Rift.Leave();
            loop.State.Rift.StartWindow(window + 1, long.MaxValue);
            for (var i = 0; i < 20; i++)
                loop.Tick();

            Assert.That(loop.Dungeons.Keys.Any(k => k.ForRift), Is.False);
            Assert.That(loop.Dungeons.Keys.Count, Is.EqualTo(2));
        }

        [Test]
        public void AKeyCanBeSalvagedByHand()
        {
            var (loop, _) = Hero();
            var key = Key(3);
            loop.State.Dungeons.Keys.Add(key);
            var shards = loop.State.Drops.Shards;

            Assert.That(loop.Dungeons.Salvage(key), Is.EqualTo(DungeonKeyResult.Done));
            Assert.That(loop.Dungeons.Salvage(key), Is.EqualTo(DungeonKeyResult.NoKey));

            Assert.That(loop.Dungeons.Keys, Is.Empty);
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(shards + Rules.SalvageShards(3)));
            Assert.That(Rules.SalvageShards(3), Is.GreaterThan(0));
        }

        [Test]
        public void ThreeKeysOfATierMergeIntoOneOfTheNextTier()
        {
            var (loop, _) = Hero(5000);
            Assume.That(Rules.MergeCount, Is.EqualTo(3));
            loop.State.Dungeons.Keys.Add(KeyAt(2, 400));
            loop.State.Dungeons.Keys.Add(KeyAt(2, 520));
            Assert.That(loop.Dungeons.Merge(2), Is.EqualTo(DungeonKeyResult.NotEnoughKeys));
            loop.State.Dungeons.Keys.Add(KeyAt(2, 450));
            var other = KeyAt(1, 200);
            loop.State.Dungeons.Keys.Add(other);

            Assert.That(loop.Dungeons.Merge(2), Is.EqualTo(DungeonKeyResult.Done));

            Assert.That(loop.Dungeons.Keys.Count, Is.EqualTo(2));
            Assert.That(loop.Dungeons.Keys, Does.Contain(other));
            var merged = loop.Dungeons.Keys.Single(k => k != other);
            Assert.That(merged.Tier, Is.EqualTo(3));
            Assert.That(merged.Stage, Is.EqualTo(520));
            Assert.That(merged.Attempts, Is.EqualTo(Rules.KeyAttempts));
            Assert.That(merged.Modifiers.Count, Is.InRange(1, Rules.MaxModifiers));
        }

        [Test]
        public void KeysDoNotMergeIntoATierBehindTheLimit()
        {
            var (loop, _) = Hero(60000);
            for (var i = 0; i < 3; i++)
                loop.State.Dungeons.Keys.Add(KeyAt(9, 40000));

            Assert.That(loop.Dungeons.Merge(9), Is.EqualTo(DungeonKeyResult.NoHigherTier));
            Assert.That(loop.Dungeons.Merge(16), Is.EqualTo(DungeonKeyResult.NoHigherTier));
            Assert.That(loop.Dungeons.Keys.Count, Is.EqualTo(3));
        }

        [Test]
        public void AKeyIsGuaranteedAfterALongDrySpell()
        {
            var (loop, _) = Hero();
            Assume.That(Rules.KeyPity, Is.GreaterThan(0));
            loop.State.Dungeons.BossesWithoutKey = Rules.KeyPity - 1;

            loop.Drops.OnBossDefeated(301, EnemyType.Boss, 301, raisesRecord: true);
            loop.Drops.Flush();

            Assert.That(loop.Dungeons.Keys.Count, Is.EqualTo(1));
            Assert.That(loop.State.Dungeons.BossesWithoutKey, Is.Zero);
        }

        [Test]
        public void AutoRunTakesTheBestFreshKeyAtOrAboveTheChosenTier()
        {
            var (loop, input) = Hero(5000, 1e60);
            var low = KeyAt(1, 200);
            var failed = KeyAt(4, 1300);
            failed.Attempts--;
            var best = KeyAt(3, 700);
            loop.State.Dungeons.Keys.Add(low);
            loop.State.Dungeons.Keys.Add(failed);
            loop.State.Dungeons.Keys.Add(best);
            loop.Dungeons.SetAuto(true, 2);

            for (var i = 0; i < 30 && !loop.Dungeons.Inside; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.Dungeons.Active, Is.SameAs(best));

            for (var i = 0; i < 6000 && loop.State.Dungeons.Completed == 0; i++)
            {
                input.PressKeys(3);
                loop.Tick();
                loop.Events.Clear();
            }
            for (var i = 0; i < 30; i++)
                loop.Tick();

            Assert.That(loop.State.Dungeons.Completed, Is.EqualTo(1));
            Assert.That(loop.Dungeons.Inside, Is.False, "the low key is under the chosen tier and the failed one waits for the player");
            Assert.That(loop.Dungeons.Keys, Is.EquivalentTo(new[] { low, failed }));
        }

        [Test]
        public void AutoRunIsOffByDefault()
        {
            var (loop, _) = Hero();
            loop.State.Dungeons.Keys.Add(Key(1));

            for (var i = 0; i < 30; i++)
                loop.Tick();

            Assert.That(loop.Dungeons.Inside, Is.False);
        }

        [Test]
        public void ALongAbsenceInsideADungeonReturnsHomeWithTheKeyAndOfflineCounts()
        {
            var (loop, _) = Hero();
            var key = Key(1);
            loop.State.Dungeons.Keys.Add(key);
            loop.Dungeons.Enter(key);

            Assert.That(loop.Dungeons.RetreatIfAway(Rules.RetreatSeconds - 1), Is.False);
            Assert.That(loop.Dungeons.Inside, Is.True);
            Assert.That(loop.Dungeons.RetreatIfAway(3600), Is.True);

            Assert.That(loop.Dungeons.Inside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(300));
            Assert.That(loop.Dungeons.Keys, Is.EquivalentTo(new[] { key }));
            Assert.That(key.Attempts, Is.EqualTo(Rules.KeyAttempts));
            Assert.That(loop.State.Dungeons.Failed, Is.Zero);
        }

        [Test]
        public void ACompletedDungeonGivesLootAndReturnsHome()
        {
            var (loop, input) = Hero();
            var key = Key(2, "thick_hide");
            loop.State.Dungeons.Keys.Add(key);
            var shards = loop.State.Drops.Shards;
            loop.Events.Clear();

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
            Assert.That(loop.Dungeons.Inside, Is.True);
            Assert.That(loop.State.Stage, Is.EqualTo(Rules.TierAt(2)!.Stage + 1));
            Assert.That(loop.Stages.RaisesRecord, Is.False);
            Assert.That(loop.Dungeons.Keys, Is.Empty);

            var dungeonDrops = 0;
            var completed = false;
            for (var i = 0; i < 3000 && loop.Dungeons.Inside; i++)
            {
                input.PressKeys(3);
                loop.Tick();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Drop?.Source == DropSource.Dungeon)
                        dungeonDrops++;
                    completed |= loop.Events[e].Kind == GameEventKind.DungeonCompleted;
                }
                loop.Events.Clear();
            }

            Assert.That(completed, Is.True);
            Assert.That(loop.Dungeons.Inside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(300));
            Assert.That(loop.State.RecordStage, Is.EqualTo(300));
            Assert.That(loop.Stages.RaisesRecord, Is.True);
            Assert.That(dungeonDrops, Is.GreaterThanOrEqualTo(Rules.TierAt(2)!.Loot.Items));
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(shards + (long)System.Math.Round(Rules.TierAt(2)!.Shards * 1.3)));
            Assert.That(loop.State.Dungeons.Completed, Is.EqualTo(1));
            Assert.That(loop.State.Dungeons.BestTier, Is.EqualTo(2));
        }

        [Test]
        public void ADeepDungeonHasTheDefenseAndElementsOfItsStage()
        {
            var (loop, _) = Hero(60000);
            var key = Key(9);
            loop.State.Dungeons.Keys.Add(key);

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));

            var defense = TestWorld.Data.Combat.Defense.Defense(loop.State.Stage, loop.State.EnemyType != EnemyType.Normal);
            Assert.That(defense, Is.GreaterThan(0));
            Assert.That(loop.State.EnemyDefense, Is.EqualTo(defense));
            Assert.That(loop.State.EnemyResists, Is.Not.EqualTo(ElementRules.None));
        }

        [Test]
        public void AFailedBossEndsTheDungeon()
        {
            var (loop, _) = Hero();
            var key = Key(1);
            loop.State.Dungeons.Keys.Add(key);
            loop.Dungeons.Enter(key);
            loop.State.BossFailsInARow = 1;

            loop.Tick();

            Assert.That(loop.Dungeons.Inside, Is.False);
            Assert.That(loop.State.Dungeons.Failed, Is.EqualTo(1));
            Assert.That(loop.State.Stage, Is.EqualTo(300));
            Assert.That(loop.Dungeons.Keys, Is.EquivalentTo(new[] { key }));
            Assert.That(key.Attempts, Is.EqualTo(Rules.KeyAttempts - 1));
        }

        [Test]
        public void ModifiersChangeTheDungeonAndRaiseTheReward()
        {
            var (plain, _) = Hero();
            var (thick, _) = Hero();
            var plainKey = Key(1);
            var thickKey = Key(1, "thick_hide", "mouse_storm");
            plain.State.Dungeons.Keys.Add(plainKey);
            thick.State.Dungeons.Keys.Add(thickKey);
            plain.Dungeons.Enter(plainKey);
            thick.Dungeons.Enter(thickKey);

            Assert.That((thick.State.EnemyMaxHp / plain.State.EnemyMaxHp).ToDouble(), Is.EqualTo(1.8).Within(1e-6));
            Assert.That(thick.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse) / plain.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse), Is.EqualTo(2).Within(1e-9));
            Assert.That(thick.Dungeons.RewardBonus(thickKey), Is.EqualTo(0.4).Within(1e-9));

            thick.Dungeons.Abandon();
            Assert.That(thick.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse), Is.EqualTo(plain.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse)).Within(1e-9));
            Assert.That(thick.Dungeons.Modifiers, Is.Empty);
        }

        [Test]
        public void DungeonsAndTheRiftDoNotMix()
        {
            var (loop, _) = Hero();
            var key = Key(1);
            loop.State.Dungeons.Keys.Add(key);
            loop.Dungeons.Enter(key);

            Assert.That(loop.Rift.Enter(long.MaxValue / 2), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.InDungeon));
            var offline = new OfflineCalculator(TestWorld.Data, loop.Hero).Simulate(loop.State, 3600);
            Assert.That(offline.Kills, Is.Zero);
        }

        [Test]
        public void ADungeonRunSurvivesSaveAndLoad()
        {
            var (loop, _) = Hero();
            var key = Key(3, "hurry");
            loop.State.Dungeons.Keys.Add(key);
            loop.State.Dungeons.Keys.Add(new DungeonKey(1, new[] { "no_focus", "tough_boss" }, 777, 2));
            loop.State.Dungeons.Keys.Add(new DungeonKey(2, new[] { "hurry" }, 30, 3, 91));
            loop.Dungeons.SetAuto(true, 4);
            loop.Dungeons.SetAutoSkip("no_focus", true);
            loop.State.Dungeons.AutoMerge = true;
            loop.State.Dungeons.BossesWithoutKey = 7;
            loop.Dungeons.Enter(key);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.Dungeons.Inside, Is.True);
            Assert.That(loaded.State.Dungeons.Active!.Tier, Is.EqualTo(3));
            Assert.That(loaded.State.Dungeons.Keys.First().Modifiers, Is.EqualTo(new[] { "no_focus", "tough_boss" }));
            Assert.That(loaded.State.Dungeons.Keys.First().Stage, Is.EqualTo(777));
            Assert.That(loaded.State.Dungeons.Keys.First().Attempts, Is.EqualTo(2));
            Assert.That(loaded.State.Dungeons.Active.Attempts, Is.EqualTo(Rules.KeyAttempts));
            Assert.That(loaded.State.Dungeons.Auto, Is.True);
            Assert.That(loaded.State.Dungeons.AutoMinTier, Is.EqualTo(4));
            Assert.That(loaded.State.Dungeons.BossesWithoutKey, Is.EqualTo(7));
            Assert.That(loaded.State.Dungeons.Keys.Last().Rift, Is.EqualTo(91));
            Assert.That(loaded.State.Dungeons.AutoMerge, Is.True);
            Assert.That(loaded.State.Dungeons.AutoSkip, Is.EqualTo(new[] { "no_focus" }));
            Assert.That(loaded.Stages.RaisesRecord, Is.False);
            loaded.Dungeons.Abandon();
            Assert.That(loaded.State.Stage, Is.EqualTo(300));
        }

        [Test]
        public void TheDungeonWindowReadsInRussian()
        {
            var loop = TestWorld.RealLoop();
            Texts.UseLanguage("ru");
            var real = TestWorld.Real.Dungeons;
            var key = new DungeonKey(2, new[] { "thick_hide", "hurry" }, 420, 2);

            Assert.That(DungeonText.Key(loop.Dungeons, key), Is.EqualTo("Ключ тира 2 · этап 421 · попыток 2 из " + real.KeyAttempts));
            Assert.That(DungeonText.Modifiers(real, key), Is.EqualTo("Толстокожие, Спешка"));
            Assert.That(DungeonText.Modifiers(real, KeyAt(1, 200)), Is.EqualTo("без модификаторов"));
            Assert.That(DungeonText.Reward(loop.Dungeons, key), Does.StartWith("Вещей ").And.EndWith("+60%"));
            Assert.That(DungeonText.Salvage(real, key), Is.EqualTo("Разобрать: Осколков " + real.SalvageShards(2)));
            Assert.That(DungeonText.AutoMerge(real, true), Does.StartWith("Автослияние включено: " + real.MergeCount));
            Assert.That(DungeonText.AutoMerge(real, false), Is.EqualTo("Автослияние выключено"));
            Assert.That(DungeonText.Count(3, 40), Is.EqualTo("Показано ключей: 3 из 40"));
            var riftKey = new DungeonKey(2, new[] { "hurry" }, 40, 3, 5);
            Assert.That(DungeonText.Key(loop.Dungeons, riftKey), Does.StartWith("Ключ Разлома тира 2 · этап 41"));
            Assert.That(DungeonText.Reward(loop.Dungeons, riftKey), Does.StartWith("Осколков Разлома ").And.EndWith("+30%"));
            Assert.That(DungeonText.Salvage(real, riftKey), Does.StartWith("Разобрать: Осколков Разлома "));
            Assert.That(DungeonText.Entry(DungeonResult.OutsideRift), Is.EqualTo("Ключ Разлома работает только в Разломе"));
            Assert.That(DungeonText.Auto(true, 3), Is.EqualTo("Ключи проходятся сами: от тира 3"));
            Assert.That(DungeonText.Auto(false, 3), Is.EqualTo("Ключи ждут вас"));
            Assert.That(DungeonText.Entry(DungeonResult.InParty), Is.EqualTo("Нельзя в общем бою отряда"));
            Assert.That(DungeonText.Change(DungeonKeyResult.NotEnoughKeys), Is.EqualTo("Не хватает ключей этого тира"));
        }

        [Test]
        public void AKeyFromAnOldSaveGetsFullAttemptsAndTheStageOfItsTier()
        {
            var state = new GameState(6) { Class = HeroClass.Runedancer, Stage = 300, HighestStage = 300, RecordStage = 300 };
            var old = new DungeonKey(2, System.Array.Empty<string>(), 0, 0);
            state.Dungeons.Keys.Add(old);

            var loop = TestWorld.Loop(state: state);

            Assert.That(old.Attempts, Is.EqualTo(Rules.KeyAttempts));
            Assert.That(loop.Dungeons.StageOf(old), Is.EqualTo(Rules.TierAt(2)!.Stage));
        }

        [Test]
        public void ThereIsNoPrestigeFromInsideADungeon()
        {
            var (loop, _) = Hero(5000);
            var key = KeyAt(4, 4000);
            loop.State.Dungeons.Keys.Add(key);
            Assume.That(loop.Prestige.Available, Is.True);

            loop.Dungeons.Enter(key);

            Assert.That(loop.Prestige.Available, Is.False);
            Assert.That(loop.Prestige.TryPrestige(), Is.False);
            loop.Dungeons.Abandon();
            Assert.That(loop.Prestige.Available, Is.True);
        }

        [Test]
        public void ALongAbsenceInsideTheRiftReturnsHomeAndKeepsTheRiftProgress()
        {
            var (loop, _) = InRift();
            loop.State.Stage = 15;

            Assert.That(loop.ReturnHomeIfAway(Rules.RetreatSeconds - 1), Is.False);
            Assert.That(loop.State.Rift.Inside, Is.True);
            Assert.That(loop.ReturnHomeIfAway(3600), Is.True);

            Assert.That(loop.State.Rift.Inside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(300));
            Assert.That(loop.State.Rift.OtherWorld!.Stage, Is.EqualTo(15));
            Assert.That(loop.Stages.RaisesRecord, Is.True);
        }

        [Test]
        public void TheKeyChanceOfAPartyIsHigherThanOfOneHeroButNotFourfold()
        {
            Assume.That(Rules.PartySplit, Is.EqualTo(0.5));

            Assert.That(2 / Rules.PartyDivisor(1), Is.EqualTo(4.0 / 3).Within(1e-9));
            Assert.That(3 / Rules.PartyDivisor(2), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(4 / Rules.PartyDivisor(3), Is.EqualTo(1.6).Within(1e-9));
        }

        [Test]
        public void DungeonsOpenAtStage150()
        {
            var state = new GameState(5) { Class = HeroClass.Runedancer, RecordStage = 100 };
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), state, new StatSheet());
            var key = Key(1);
            loop.State.Dungeons.Keys.Add(key);

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Locked));
            loop.State.RecordStage = 150;
            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
        }
    }
}
