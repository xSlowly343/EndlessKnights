using System;
using System.Linq;
using System.Reflection;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftPartyOnlyTests
    {
        private static readonly long Inside = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static GameLoop Home()
        {
            var loop = TestWorld.Loop(state: new GameState { Stage = 20, HighestStage = 20, RecordStage = 20 });
            loop.Tick();
            return loop;
        }

        private static void Enter(GameLoop loop) =>
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));

        private static ModifierLayer LayerOf(object system) =>
            (ModifierLayer)system.GetType().GetProperty("Layer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system)!;

        [Test]
        public void EveryPeacefulAndClanLayerFallsSilentInsideAndSpeaksAtHome()
        {
            var loop = Home();
            var systems = new object[] { loop.Museum, loop.Cooking, loop.Alchemy, loop.ClanCamp, loop.Gates, loop.Chronicle, loop.ClanTree, loop.Mentor };
            foreach (var system in systems)
            {
                var layer = LayerOf(system);
                var before = loop.Hero.Value(CombatStats.CritChance, 0);
                layer.Set(new[] { new Modifier(CombatStats.CritChance, ModifierKind.Flat, 0.07) });
                Assert.That(loop.Hero.Value(CombatStats.CritChance, 0), Is.EqualTo(before + 0.07).Within(1e-9), system.GetType().Name + " works at home");
            }

            Enter(loop);
            foreach (var system in systems)
                Assert.That(LayerOf(system).Muted, Is.True, system.GetType().Name + " is silent in the Rift");

            loop.Rift.Leave();
            foreach (var system in systems)
                Assert.That(LayerOf(system).Muted, Is.False, system.GetType().Name + " speaks again at home");
        }

        [Test]
        public void TheFriendGhostFightsAtHomeButNotInTheRift()
        {
            var loop = Home();
            loop.Ghost.Use(new GhostSnapshot("friend", "Аня", HeroClass.Whisperer, 0.4, 1));
            loop.Tick();
            Assert.That(loop.Ghost.Fighting, Is.True);
            Assert.That(loop.Ghost.Damage.Sign, Is.GreaterThan(0));

            Enter(loop);
            var damage = loop.Ghost.Damage;
            for (var i = 0; i < 20; i++)
                loop.Tick();
            Assert.That(loop.Ghost.Fighting, Is.False, "a friend is not the party");
            Assert.That((loop.Ghost.Damage - damage).Sign, Is.Zero);

            loop.Rift.Leave();
            loop.Tick();
            Assert.That(loop.Ghost.Fighting, Is.True);
        }

        [Test]
        public void HomeMuseumCollectionsGiveNoDamageInTheRift()
        {
            var loop = Home();
            loop.State.Museum.Done.Add("test_collection_a");
            loop.State.Museum.Done.Add("test_collection_b");
            loop.Hero.Add(new Modifier(CombatStats.DamagePerMuseumCollection, ModifierKind.Flat, 0.1));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.None), Is.EqualTo(1.2).Within(1e-9));

            Enter(loop);
            Assert.That(loop.State.MuseumCollectionsInPlay, Is.Zero);
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.None), Is.EqualTo(1).Within(1e-9));

            loop.Rift.Leave();
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.None), Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void ASetFoundInTheRiftDoesNotGrowFromTheHomeMuseum()
        {
            var loop = Home();
            var chronicle = TestWorld.Data.Items.Sets.First(set => set.PerMuseumCollection.Count > 0);
            loop.State.Museum.Done.Add("test_collection_a");
            loop.State.Museum.Done.Add("test_collection_b");
            Enter(loop);
            var stat = chronicle.PerMuseumCollection[0].Stat;
            var tags = chronicle.PerMuseumCollection[0].Tags;
            var without = loop.Hero.Value(stat, 1, tags);
            foreach (var piece in chronicle.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(chronicle.Id, piece));
            loop.Gear.Rebuild();
            var bonusOnly = loop.Hero.Value(stat, 1, tags);
            loop.State.Museum.Done.Add("test_collection_c");
            loop.Gear.Rebuild();
            Assert.That(loop.Hero.Value(stat, 1, tags), Is.EqualTo(bonusOnly).Within(1e-12), "more home collections change nothing inside");
            Assert.That(bonusOnly, Is.GreaterThanOrEqualTo(without));
        }

        [Test]
        public void CompanionStrikesFromTheHomeTreeStayHome()
        {
            var loop = Home();
            Assert.That(loop.Paths.CompanionHits, Is.Zero);
            var source = TreeLayer(loop);
            source.Set(new[] { new Modifier(CombatStats.CompanionHitsPerSecond, ModifierKind.Flat, 2).InPool(ModifierPool.Tree) });
            for (var i = 0; i < 10; i++)
                loop.Tick();
            var homeHits = loop.Paths.CompanionHits;
            Assert.That(homeHits, Is.GreaterThan(0), "the home hero's companion strikes at home");

            Enter(loop);
            Assert.That(loop.Hero.Value(CombatStats.CompanionHitsPerSecond, 0, StatTags.Minion), Is.Zero, "the Rift hero has an empty tree");
            for (var i = 0; i < 10; i++)
                loop.Tick();
            Assert.That(loop.Paths.CompanionHits, Is.EqualTo(homeHits));
        }

        [Test]
        public void HomeSetsMadeForTheRiftAreTheOnlyHomeSetsThatWorkInside()
        {
            var loop = Home();
            var items = TestWorld.Data.Items;
            var depths = items.Sets.First(set => set.CarriedToRift && set.Rarity == ItemRarity.Rare);
            var plain = items.Sets.First(set => set.Ordinary && !set.CarriedToRift && !set.Peaceful && set.Rarity == ItemRarity.Rare && !set.Jewelry);
            foreach (var set in new[] { depths, plain })
            {
                foreach (var piece in set.Pieces)
                    loop.State.SetCollection.Add(SetDef.PieceKey(set.Id, piece));
            }
            loop.Gear.Rebuild();
            Assert.That(items.Sets.Where(set => set.CarriedToRift).All(set => set.Bonus.All(line => line.When == ModifierCondition.InRift)), Is.True, "a set carried into the Rift works only there");

            Enter(loop);
            Assert.That(loop.Gear.Collected(depths), Is.False, "it is not in the Rift hero's collection");
            Assert.That(loop.Gear.CarriedFromHome(depths), Is.True);
            Assert.That(loop.Gear.CarriedFromHome(plain), Is.False);
            var homeScale = items.MasteryScale(items.MasteryLevel(0));
            foreach (var line in depths.Bonus)
                Assert.That(loop.Gear.Modifiers, Has.Member(line.WithValue(line.Value * homeScale)), "the home Rift set works inside");
            foreach (var line in plain.Bonus)
                Assert.That(loop.Gear.Modifiers, Has.None.EqualTo(line), "an ordinary home set stays home");

            loop.Rift.Leave();
            Assert.That(loop.Gear.CarriedFromHome(depths), Is.False);
            Assert.That(loop.Gear.Collected(depths), Is.True);
        }

        private static ModifierLayer TreeLayer(GameLoop loop) =>
            (ModifierLayer)typeof(GameLoop).GetField("_treeLayer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loop)!;
    }
}
