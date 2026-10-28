using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class JewelTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;
        private static PassiveTree Tree => TestWorld.Data.Tree;

        private static JewelItem Jewel(params (string Id, double Value)[] affixes)
        {
            var list = new List<GearAffix>();
            foreach (var (id, value) in affixes)
            {
                Assert.That(Items.TryGetAffix(id, out var def), Is.True, id);
                list.Add(new GearAffix(def, 1, value));
            }
            return new JewelItem(ItemRarity.Rare, 1, list);
        }

        private static (GameLoop Loop, string Socket) WithOpenSocket()
        {
            var state = new GameState(8) { Class = HeroClass.Viking, Tree = new TreeAllocation("viking") };
            var loop = TestWorld.Loop(state: state);
            var path = PathToJewel(Tree.StartOf("viking")!);
            foreach (var node in path)
                state.Tree.Restore(node.Id, node.Type == TreeNodeType.Attribute ? Tree.Attributes[0] : null, node.Type == TreeNodeType.Mastery ? 0 : (int?)null);
            state.Tree.PointsEarned = path.Count;
            loop.RebuildTree();
            return (loop, path[path.Count - 1].Id);
        }

        private static List<TreeNodeDef> PathToJewel(TreeNodeDef start)
        {
            var parent = new Dictionary<TreeNodeDef, TreeNodeDef?> { [start] = null };
            var queue = new Queue<TreeNodeDef>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node.Type == TreeNodeType.Jewel)
                {
                    var path = new List<TreeNodeDef>();
                    for (var at = node; at != null && at != start; at = parent[at])
                        path.Insert(0, at);
                    return path;
                }
                foreach (var next in node.Neighbours)
                {
                    if (parent.ContainsKey(next) || next.Type == TreeNodeType.Start || next.Locked)
                        continue;
                    parent[next] = node;
                    queue.Enqueue(next);
                }
            }
            throw new AssertionException("no jewel socket reachable");
        }

        private static string AnyJewelSocket() => Tree.Nodes.First(n => n.Type == TreeNodeType.Jewel).Id;

        [Test]
        public void JewelsRollFromTheirOwnAffixPool()
        {
            var rng = new Rng(4);
            var rare = Items.RollJewel(ItemRarity.Rare, 6, rng);
            var epic = Items.RollJewel(ItemRarity.Epic, 6, rng);
            var plain = Items.RollJewel(ItemRarity.Simple, 6, rng);
            var mythic = Items.RollJewel(ItemRarity.Mythic, 6, rng);

            Assert.That(rare.Affixes.Count, Is.EqualTo(3));
            Assert.That(epic.Affixes.Count, Is.EqualTo(4));
            Assert.That(plain.Affixes.Count, Is.EqualTo(1));
            Assert.That(mythic.Affixes.Count, Is.EqualTo(Items.JewelMaxAffixes));
            foreach (var jewel in new[] { rare, epic, plain, mythic })
                Assert.That(jewel.Affixes.All(a => Items.JewelAffixes.Contains(a.Def)), Is.True);
        }

        [Test]
        public void GearNeverRollsJewelAffixes()
        {
            var rng = new Rng(9);
            for (var i = 0; i < 300; i++)
            {
                var item = Items.Roll(ItemSlot.Helmet, null, ItemRarity.Mythic, 6, rng);
                Assert.That(item.Affixes.Any(a => Items.JewelAffixes.Contains(a.Def)), Is.False);
            }
        }

        [Test]
        public void SocketedJewelWorksOnlyInAnAllocatedSocket()
        {
            var (loop, socket) = WithOpenSocket();
            var jewel = Jewel(("socketed_boss_damage", 0.1));
            loop.State.Jewels.Bag.Add(jewel);
            var closed = Tree.Nodes.First(n => n.Type == TreeNodeType.Jewel && !loop.State.Tree.IsAllocated(Tree, n.Id)).Id;

            Assert.That(loop.Jewels.Socket(closed, jewel), Is.False);
            Assert.That(loop.Jewels.Socket(socket, jewel), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(loop.State.Jewels.Bag, Is.Empty);

            loop.State.Tree.Remove(socket);
            loop.RebuildTree();
            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.Jewels.In(socket), Is.SameAs(jewel));
        }

        [Test]
        public void UnsocketingReturnsTheJewelToTheBag()
        {
            var (loop, socket) = WithOpenSocket();
            var first = Jewel(("socketed_key_damage", 0.1));
            var second = Jewel(("socketed_gold", 0.1));
            loop.State.Jewels.Bag.Add(first);
            loop.State.Jewels.Bag.Add(second);

            loop.Jewels.Socket(socket, first);
            loop.Jewels.Socket(socket, second);
            Assert.That(loop.State.Jewels.Bag, Is.EqualTo(new[] { first }));

            Assert.That(loop.Jewels.Unsocket(socket), Is.True);
            Assert.That(loop.State.Jewels.Bag, Is.EquivalentTo(new[] { first, second }));
            Assert.That(loop.Jewels.Unsocket(socket), Is.False);
        }

        [Test]
        public void JewelFacetStrengthensSocketedJewels()
        {
            var (loop, socket) = WithOpenSocket();
            var jewel = Jewel(("socketed_boss_damage", 0.1));
            loop.State.Jewels.Bag.Add(jewel);
            loop.Jewels.Socket(socket, jewel);

            loop.State.Artifacts["jewel_facet"] = 10;
            loop.Artifacts.Rebuild();

            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.EqualTo(1 + 0.1 * 1.3).Within(1e-9));
        }

        [Test]
        public void JewelsSurviveSaveAndLoad()
        {
            var (loop, socket) = WithOpenSocket();
            var socketed = Jewel(("socketed_crit_chance", 0.01), ("socketed_strength", 5));
            var spare = Jewel(("socketed_focus", 0.05));
            loop.State.Jewels.Bag.Add(socketed);
            loop.State.Jewels.Bag.Add(spare);
            loop.Jewels.Socket(socket, socketed);
            loop.State.Jewels.Found = 7;

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Jewels.Found, Is.EqualTo(7));
            Assert.That(loaded.Jewels.Bag.Single().Affixes.Single().Def.Id, Is.EqualTo("socketed_focus"));
            Assert.That(loaded.Jewels.Sockets[socket].Affixes.Select(a => a.Def.Id), Is.EqualTo(new[] { "socketed_crit_chance", "socketed_strength" }));
            Assert.That(loaded.Jewels.Sockets[socket].Affixes[1].Value, Is.EqualTo(5));
        }

        [Test]
        public void GuardiansAndSpecialBossesDropJewels()
        {
            var loop = TestWorld.Loop(state: TestWorld.Classless(new GameState(12) { Class = HeroClass.Viking }));
            var stage = 2L * TestWorld.Data.Stages.BiomeLength;
            var jewels = 0;
            for (var i = 0; i < 60; i++)
            {
                loop.Events.Clear();
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, stage, raisesRecord: true);
                loop.Drops.Flush();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Kind == GameEventKind.JewelDropped)
                        jewels++;
                }
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(jewels, Is.InRange(15, 45));
            Assert.That(loop.State.Jewels.Bag.Count, Is.EqualTo(jewels));
            Assert.That(loop.State.Jewels.Found, Is.EqualTo(jewels));
            Assert.That(loop.State.Jewels.Bag.All(j => j.Rarity >= ItemRarity.Rare), Is.True);
        }

        [Test]
        public void LongStreakDamageNeedsTwoMinutesWithoutPauses()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.DamageLongStreak, ModifierKind.Increased, 0.2));
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState(3) { Class = HeroClass.Viking }), hero);
            var conditions = TestWorld.Data.Combat.Conditions;
            var ticks = (int)(conditions.LongStreakSeconds * GameLoop.TicksPerSecond);

            for (var i = 0; i < ticks - 5; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1).Within(1e-9));

            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1.2).Within(1e-9));

            for (var i = 0; i < (int)(conditions.LongStreakPauseSeconds * GameLoop.TicksPerSecond) + 1; i++)
                loop.Tick();
            Assert.That(loop.State.InputStreakTicks, Is.Zero);
        }

        [Test]
        public void ShortPausesDoNotBreakTheStreak()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState(3) { Class = HeroClass.Viking }));
            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }

            Assert.That(loop.State.InputStreakTicks, Is.EqualTo(100));
        }
    }
}
