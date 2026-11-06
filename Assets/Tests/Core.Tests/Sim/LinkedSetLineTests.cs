using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LinkedSetLineTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static SetDef Set(string id) => Items.Sets.Single(s => s.Id == id);

        private static GameLoop Runedancer(StatSheet? hero = null) =>
            TestWorld.Loop(null, new GameState(7) { Class = HeroClass.Runedancer }, hero);

        private static void Collect(GameLoop loop, string setId)
        {
            foreach (var piece in Set(setId).Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(setId, piece));
            loop.Gear.Rebuild();
        }

        private static void Wear(GameLoop loop, string setId, ItemSlot slot)
        {
            var set = Set(setId);
            var item = Items.RollSetPiece(set, slot, null, set.Rarity, 1, new Rng(3));
            loop.State.Drops.Gear.Add(item);
            loop.Gear.Equip(item);
        }

        private static bool Holds(GameLoop loop, ModifierCondition condition) => (loop.Hero.Conditions & ModifierConditions.Bit(condition)) != 0;

        private static void Reach(GameLoop loop, long stage)
        {
            loop.State.Stage = stage;
            loop.State.HighestStage = stage;
            loop.State.RecordStage = System.Math.Max(loop.State.RecordStage, stage);
        }

        [Test]
        public void FourLinkedLinesHaveASetInEveryRarity()
        {
            foreach (var line in new[] { "echo_legacy", "reforged", "edge_chronicle", "party_brotherhood" })
            {
                var sets = Items.Sets.Where(s => s.Theme == line).ToList();
                Assert.That(sets.Count, Is.EqualTo(6), line);
                Assert.That(sets.All(s => !s.Peaceful), Is.True, line);
            }
            Assert.That(Set("echo_legacy_rift").PieceLines(ItemSlot.Gloves)[0].Value, Is.EqualTo(190));
            Assert.That(Set("party_brotherhood_rare").EchoBit, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TheHeadStartIsAShareOfThePrestigeStageCappedAtNinetyPercent()
        {
            var loop = Runedancer();
            Reach(loop, 1000);
            Assert.That(loop.Prestige.StartStage, Is.EqualTo(1));

            Collect(loop, "echo_legacy_rare");
            Assert.That(loop.Prestige.StartStage, Is.EqualTo(31));

            var greedy = new StatSheet();
            greedy.Add(new Modifier(CombatStats.PrestigeStartShare, ModifierKind.Flat, 5));
            var capped = Runedancer(greedy);
            Reach(capped, 1000);
            Assert.That(capped.Prestige.StartStage, Is.EqualTo(901));
        }

        [Test]
        public void PrestigeNearTheRecordAddsAStageRushUpToTheGlovesCap()
        {
            var loop = Runedancer();
            Wear(loop, "echo_legacy_rare", ItemSlot.Gloves);
            Assert.That(loop.Prestige.RushCap, Is.EqualTo(25));

            Reach(loop, 500);
            loop.State.HighestStage = 400;
            Assert.That(loop.Prestige.TryPrestige(), Is.True);
            Assert.That(loop.State.StageRush, Is.Zero, "400 is below 99% of the 500 record");

            for (var i = 0; i < 30; i++)
            {
                Reach(loop, 500);
                Assert.That(loop.Prestige.TryPrestige(), Is.True);
            }
            Assert.That(loop.State.StageRush, Is.EqualTo(25));
            Assert.That(loop.State.Stage, Is.EqualTo(26));
        }

        [Test]
        public void ThePortalSkipsStagesButStopsAtTheGuardianAndTheRecord()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.PortalChance, ModifierKind.Flat, 1));
            var loop = Runedancer(hero);
            loop.State.RecordStage = 1000;
            loop.State.Stage = 3;
            loop.State.HighestStage = 3;
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.One);

            loop.Stages.OnEnemyKilled();

            var guardian = TestWorld.Data.Stages.BiomeLength;
            Assert.That(loop.State.Stage, Is.EqualTo(System.Math.Min(3 + 50, guardian)));
            Assert.That(loop.State.KillsOnStage, Is.Zero);
            loop.Stages.OnEnemyKilled();
            Assert.That(loop.State.Stage, Is.EqualTo(System.Math.Min(3 + 50, guardian)), "the guardian stage is never skipped");
        }

        [Test]
        public void SecondWindHitsHarderWellBelowTheRecord()
        {
            var line = Set("echo_legacy_rare").PieceLines(ItemSlot.Weapon)[0];
            Assert.That(line.When, Is.EqualTo(ModifierCondition.BelowRecord));
            var hero = new StatSheet();
            hero.Add(line);
            var loop = Runedancer(hero);
            Reach(loop, 100);
            loop.Tick();
            var atRecord = loop.Hero.Value(CombatStats.Damage, 100);
            loop.State.Stage = 50;
            loop.Tick();
            Assert.That(Holds(loop, ModifierCondition.BelowRecord), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) - atRecord, Is.EqualTo(20).Within(1e-9));
        }

        [Test]
        public void ReforgedFollowsTheTemperOfWornItems()
        {
            var loop = Runedancer();
            Wear(loop, "reforged_rare", ItemSlot.Helmet);
            Wear(loop, "reforged_rare", ItemSlot.Chest);
            Collect(loop, "reforged_rare");
            var max = TestWorld.Data.Forge.MaxTemper;
            loop.State.Weapons.Held!.Forge.Temper = max;
            loop.Gear.In(GearSlot.Helmet)!.Forge.Temper = max;
            loop.Tick();
            Assert.That(loop.State.WornTemperPercent, Is.EqualTo(max * 100 * 2 / 3).Within(1e-9), "weapon, helmet and an untempered chest");
            Assert.That(Holds(loop, ModifierCondition.AllTempered), Is.False);

            loop.Gear.In(GearSlot.Chest)!.Forge.Temper = max;
            loop.Tick();
            Assert.That(Holds(loop, ModifierCondition.AllTempered), Is.True);
            Assert.That(loop.State.Perk(MuseumPerk.TemperStep), Is.GreaterThanOrEqualTo(0.05));
        }

        [Test]
        public void EdgeChronicleGrowsWithEveryMuseumCollection()
        {
            var loop = Runedancer();
            Collect(loop, "edge_chronicle_rare");
            loop.Tick();
            var gold = loop.Hero.Value(CombatStats.Gold, 100, StatTags.Gold);
            loop.State.Museum.Done.Add("a");
            loop.State.Museum.Done.Add("b");
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Gold, 100, StatTags.Gold) - gold, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void BrotherhoodEchoesForEveryAllyWithTheSameSet()
        {
            var loop = Runedancer();
            var set = Set("party_brotherhood_rare");
            Collect(loop, set.Id);
            Assert.That(loop.Gear.EchoMask & (1u << set.EchoBit), Is.Not.Zero);
            var alone = loop.Hero.Value(CombatStats.Damage, 100);

            loop.Gear.SetAllyEcho(1, 1u << set.EchoBit);
            loop.Gear.SetAllyEcho(2, 1u << set.EchoBit);
            loop.Gear.SetAllyEcho(3, 1u << Set("party_brotherhood_epic").EchoBit);

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) - alone, Is.EqualTo(4).Within(1e-9));
        }
    }
}
