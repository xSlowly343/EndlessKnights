using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DungeonTests
    {
        private static DungeonRules Rules => TestWorld.Data.Dungeons;

        private static (GameLoop Loop, FakeInputSource Input) Hero(long stage = 300)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            var state = new GameState(6) { Class = HeroClass.Viking, Stage = stage, HighestStage = stage, RecordStage = stage };
            return (TestWorld.Loop(input, state, hero), input);
        }

        private static DungeonKey Key(int tier, params string[] modifiers) => new DungeonKey(tier, modifiers);

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
            Assert.That(loop.Dungeons.Keys.All(k => k.Modifiers.Count <= Rules.MaxModifiers), Is.True);
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
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(shards + (long)System.Math.Round(Rules.TierAt(2)!.Shards * 1.2)));
            Assert.That(loop.State.Dungeons.Completed, Is.EqualTo(1));
            Assert.That(loop.State.Dungeons.BestTier, Is.EqualTo(2));
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

            Assert.That((thick.State.EnemyMaxHp / plain.State.EnemyMaxHp).ToDouble(), Is.EqualTo(1.4).Within(1e-6));
            Assert.That(thick.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse) / plain.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse), Is.EqualTo(2).Within(1e-9));
            Assert.That(thick.Dungeons.RewardBonus(thickKey), Is.EqualTo(0.3).Within(1e-9));

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
            loop.State.Dungeons.Keys.Add(Key(1, "no_focus", "tough_boss"));
            loop.Dungeons.Enter(key);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.Dungeons.Inside, Is.True);
            Assert.That(loaded.State.Dungeons.Active!.Tier, Is.EqualTo(3));
            Assert.That(loaded.State.Dungeons.Keys.Single().Modifiers, Is.EqualTo(new[] { "no_focus", "tough_boss" }));
            Assert.That(loaded.Stages.RaisesRecord, Is.False);
            loaded.Dungeons.Abandon();
            Assert.That(loaded.State.Stage, Is.EqualTo(300));
        }

        [Test]
        public void DungeonsOpenAtStage150()
        {
            var state = new GameState(5) { Class = HeroClass.Viking, RecordStage = 100 };
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), state, new StatSheet());
            var key = Key(1);
            loop.State.Dungeons.Keys.Add(key);

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Locked));
            loop.State.RecordStage = 150;
            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
        }
    }
}
