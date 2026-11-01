using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassTreeViewTests
    {
        private static readonly ClassTree Huscarl = ClassTreeLoader.Load(
            TreeTemplates.Load(TestData.ReadGameDataFile(TreeTemplates.FileName)), "runedancer", TestData.ReadGameDataFile(ClassTreeLoader.FileNameOf("runedancer")));

        [Test]
        public void HoveringANodeLightsItsParentsAndEveryNodeThatGrowsFromIt()
        {
            var chain = ClassTreeView.Chain(Huscarl, "runedancer_r2c0");

            Assert.That(chain, Is.EquivalentTo(new[] { "runedancer_r1c0", "runedancer_r2c0", "runedancer_r3c4", "runedancer_r5c0", "runedancer_r6c0" }));
            Assert.That(ClassTreeView.Chain(Huscarl, "runedancer_r6c0"), Is.EquivalentTo(new[] { "runedancer_r1c0", "runedancer_r2c0", "runedancer_r5c0", "runedancer_r6c0" }),
                "from the top only the way down is lit, not the side branches of the parents");
            Assert.That(ClassTreeView.Chain(Huscarl, "runedancer_nope"), Is.Empty);
        }

        [Test]
        public void ThePreviewShowsWhatTheNextRankAdds()
        {
            var allocation = new ClassTreeAllocation("runedancer") { PointsEarned = 10 };
            var axe = Huscarl["runedancer_r1c0"].Template;

            var first = ClassTreeView.Next(Huscarl, allocation, "runedancer_r1c0")!.Value;
            Assert.That(first.Rank, Is.EqualTo(1));
            Assert.That(first.Line, Is.SameAs(axe.First));
            Assert.That(first.Strengthens, Is.False);

            allocation.Raise(Huscarl, "runedancer_r1c0");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_r1c0")!.Value.Strengthens, Is.True, "rank 2 strengthens the first line");
            allocation.Raise(Huscarl, "runedancer_r1c0");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_r1c0")!.Value.Line, Is.SameAs(axe.Second));
            allocation.Raise(Huscarl, "runedancer_r1c0");
            allocation.Raise(Huscarl, "runedancer_r1c0");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_r1c0")!.Value.Line, Is.SameAs(axe.Third));
            allocation.Raise(Huscarl, "runedancer_r1c0");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_r1c0"), Is.Null, "nothing after rank 5");
        }
    }
}
