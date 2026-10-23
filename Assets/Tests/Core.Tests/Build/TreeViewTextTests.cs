using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class TreeViewTextTests
    {
        private static PassiveTree Tree => TestWorld.Data.Tree;

        private static GameLoop Swordsman(int points)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
            loop.State.Tree.PointsEarned = points;
            return loop;
        }

        [Test]
        public void DescribesModifierKinds()
        {
            Assert.That(ModifierText.Describe(new Modifier(new StatId("damage"), ModifierKind.Increased, 0.05)), Is.EqualTo("+5% урон"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("damage"), ModifierKind.More, 0.08)), Is.EqualTo("ещё +8% урон"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("strength"), ModifierKind.Flat, 10)), Is.EqualTo("+10 Сила"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("critMultiplier"), ModifierKind.Flat, 0.2)), Is.EqualTo("+20% множитель крита"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("bossTimerSeconds"), ModifierKind.Flat, -10)), Is.EqualTo("-10 с таймер босса"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("damage"), ModifierKind.Increased, 0.1, StatTags.Click)), Is.EqualTo("+10% урон от кликов"));
        }

        [Test]
        public void EveryTreeStatHasARussianLabel()
        {
            var stats = Tree.Nodes.SelectMany(node => node.Modifiers.Concat(node.SharedModifiers).Concat(node.Options.Concat(node.SharedOptions).SelectMany(option => option)))
                .Select(modifier => modifier.Stat).Distinct();

            Assert.That(stats.Where(stat => ModifierText.Label(stat) == stat.Name), Is.Empty);
        }

        [Test]
        public void OfferedNodeSaysItCanBeTakenAndOffersTake()
        {
            var loop = Swordsman(1);
            var node = Tree["ronin_ring_1"];

            var lines = TreeNodeText.Describe(Tree, loop.State.Tree, node, null, NumberFormatSettings.Default);
            var actions = TreeAction.For(Tree, loop.State.Tree, node);

            Assert.That(lines[0], Does.StartWith(node.NameRu));
            Assert.That(lines.Last(), Is.EqualTo("Можно взять"));
            Assert.That(actions.Select(action => action.Kind), Is.EqualTo(new[] { TreeActionKind.Allocate }));
        }

        [Test]
        public void FarNodeIsNotConnectedAndHasNoActions()
        {
            var loop = Swordsman(1);
            var node = Tree["metalhead_ring_0"];

            Assert.That(TreeNodeText.Describe(Tree, loop.State.Tree, node, null, NumberFormatSettings.Default).Last(), Is.EqualTo("Нет связи со взятыми узлами"));
            Assert.That(TreeAction.For(Tree, loop.State.Tree, node), Is.Empty);
        }

        [Test]
        public void AttributeNodeOffersEveryAttribute()
        {
            var loop = Swordsman(50);
            TreePaths.Allocate(loop, Tree, TreeAllocationTests.ToAttribute);
            var node = Tree[TreeAllocationTests.FirstAttribute];

            var actions = TreeAction.For(Tree, loop.State.Tree, node);

            Assert.That(actions.Select(action => action.Attribute), Is.EqualTo(Tree.Attributes));
            var chosen = actions[0];
            Assert.That(loop.AllocateNode(node.Id, chosen.Attribute, chosen.MasteryOption), Is.EqualTo(AllocationResult.Allocated));
        }

        [Test]
        public void TakenLeafCanBeRefundedAndShowsItsPrice()
        {
            var loop = Swordsman(1);
            loop.AllocateNode("ronin_ring_1");
            var node = Tree["ronin_ring_1"];

            var lines = TreeNodeText.Describe(Tree, loop.State.Tree, node, loop.RefundCost(node.Id), NumberFormatSettings.Default);

            Assert.That(lines.Last(), Does.StartWith("Взят · вернуть за "));
            Assert.That(TreeAction.For(Tree, loop.State.Tree, node).Select(action => action.Kind), Is.EqualTo(new[] { TreeActionKind.Refund }));
        }

        [Test]
        public void ViewportStartsWithTheWholeTreeAndClampsZoom()
        {
            var viewport = new TreeViewport(Tree.Nodes, 430.0 / 360.0);
            var whole = viewport.VisibleHeight;

            foreach (var node in Tree.Nodes)
            {
                Assert.That(System.Math.Abs(node.X - viewport.CentreX), Is.LessThanOrEqualTo(viewport.VisibleWidth / 2));
                Assert.That(System.Math.Abs(node.Y - viewport.CentreY), Is.LessThanOrEqualTo(viewport.VisibleHeight / 2));
            }

            viewport.ZoomOut();
            Assert.That(viewport.VisibleHeight, Is.EqualTo(whole));
            for (var i = 0; i < 20; i++)
                viewport.ZoomIn();
            Assert.That(viewport.VisibleHeight, Is.EqualTo(TreeViewport.MinVisibleHeight));
        }

        [Test]
        public void ViewportResizedForTheWidePanelKeepsTheZoomAndShowsMoreAcross()
        {
            var viewport = new TreeViewport(Tree.Nodes, 464.0 / 250.0);
            viewport.FocusOn(0, 0, 30);

            viewport.Resize(944.0 / 290.0);

            Assert.That(viewport.VisibleHeight, Is.EqualTo(30));
            Assert.That(viewport.VisibleWidth, Is.EqualTo(30 * 944.0 / 290.0).Within(1e-9));
        }

        [Test]
        public void ViewportResizeClampsAZoomedOutView()
        {
            var viewport = new TreeViewport(Tree.Nodes, 1);
            var whole = viewport.VisibleHeight;

            viewport.Resize(4);

            Assert.That(viewport.VisibleHeight, Is.LessThanOrEqualTo(whole));
            foreach (var node in Tree.Nodes)
                Assert.That(System.Math.Abs(node.X - viewport.CentreX), Is.LessThanOrEqualTo(viewport.VisibleWidth / 2));
        }

        [Test]
        public void ViewportMapsTheCentreAndFindsTheNearestNode()
        {
            var viewport = new TreeViewport(Tree.Nodes, 1);
            var start = Tree["ronin_start"];
            viewport.FocusOn(start.X, start.Y, 10);

            var (x, y) = viewport.ToTree(0.5, 0.5);

            Assert.That(x, Is.EqualTo(start.X).Within(1e-9));
            Assert.That(y, Is.EqualTo(start.Y).Within(1e-9));
            Assert.That(TreeViewport.NodeAt(Tree.Nodes, x + 0.1, y, 0.5), Is.SameAs(start));
            Assert.That(TreeViewport.NodeAt(Tree.Nodes, 1000, 1000, 0.5), Is.Null);
        }

        [Test]
        public void DualNodeShowsBothLinesAndMarksTheOneThatWorks()
        {
            var node = Tree["viking_resource_0"];

            var forRonin = TreeNodeText.Describe(Tree, Swordsman(1).State.Tree, node, null, NumberFormatSettings.Default);
            var forViking = TreeNodeText.Describe(Tree, new Game.Core.Build.TreeAllocation("viking"), node, null, NumberFormatSettings.Default);

            Assert.That(forRonin[1], Is.EqualTo(TreeNodeText.OwnPrefix + "+6% накопление ресурса"));
            Assert.That(forRonin[2], Is.EqualTo(TreeNodeText.ActiveMark + TreeNodeText.SharedPrefix + "+2% накопление ресурса"));
            Assert.That(forViking[1], Is.EqualTo(TreeNodeText.ActiveMark + TreeNodeText.OwnPrefix + "+6% накопление ресурса"));
            Assert.That(forViking[2], Is.EqualTo(TreeNodeText.SharedPrefix + "+2% накопление ресурса"));
        }
    }
}
