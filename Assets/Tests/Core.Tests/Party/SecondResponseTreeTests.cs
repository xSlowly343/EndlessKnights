using System.Linq;
using Game.Core.Build;
using Game.Core.Input;
using Game.Core.Tests.Build;
using Game.Core.Content.Synergy;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SecondResponseTreeTests
    {
        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static TreeNodeDef[] SecondResponseNodes(string className) =>
            TestWorld.Data.Tree.Nodes
                .Where(node => node.ScopeClass == className && node.Modifiers.Any(modifier => modifier.Stat == Rules.SecondResponseStat))
                .ToArray();

        [Test]
        public void EveryClassHasOneNotableNearItsStartThatOpensTheSecondResponse()
        {
            foreach (var response in Rules.Responses.Where(response => response.Second))
            {
                var className = HeroClassNames.Of(response.HeroClass);
                var nodes = SecondResponseNodes(className);
                Assert.That(nodes.Length, Is.EqualTo(1), className);
                Assert.That(nodes[0].Type, Is.EqualTo(TreeNodeType.Notable), className);
                Assert.That(TreePaths.To(TestWorld.Data.Tree, className, nodes[0].Id).Count, Is.LessThanOrEqualTo(8), className + ": the notable is close to the start");
            }
        }

        [Test]
        public void AllocatingTheNotableWakesTheSecondResponseOnTheAllyMark()
        {
            for (var index = 0; index < Rules.Responses.Count; index++)
            {
                var response = Rules.Responses[index];
                if (!response.Second)
                    continue;
                var className = HeroClassNames.Of(response.HeroClass);
                var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = response.HeroClass });
                loop.State.Tree.PointsEarned = 120;
                foreach (var step in TreePaths.To(TestWorld.Data.Tree, className, SecondResponseNodes(className)[0].Id))
                {
                    var result = TestWorld.Data.Tree[step].Type == TreeNodeType.Attribute ? loop.AllocateNode(step, attribute: "strength") : loop.AllocateNode(step);
                    Assert.That(result, Is.EqualTo(AllocationResult.Allocated), className + " " + step);
                }
                loop.Synergy.SetAlly(0, true, HeroClass.Nomad, 1 << response.Mark);

                loop.Tick();

                Assert.That(loop.Synergy.ResponseActive(index), Is.True, className);
            }
        }
    }
}
