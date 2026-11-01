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
            var chain = ClassTreeView.Chain(Huscarl, "runedancer_heavy_swing");

            Assert.That(chain, Is.EquivalentTo(new[] { "runedancer_axe", "runedancer_heavy_swing", "runedancer_saga_song", "runedancer_jarl_axe", "runedancer_north_wind" }));
            Assert.That(ClassTreeView.Chain(Huscarl, "runedancer_north_wind"), Is.EquivalentTo(new[] { "runedancer_axe", "runedancer_heavy_swing", "runedancer_jarl_axe", "runedancer_north_wind" }),
                "from the top only the way down is lit, not the side branches of the parents");
            Assert.That(ClassTreeView.Chain(Huscarl, "runedancer_nope"), Is.Empty);
        }

        [Test]
        public void ThePreviewShowsWhatTheNextRankAdds()
        {
            var allocation = new ClassTreeAllocation("runedancer") { PointsEarned = 10 };
            var axe = Huscarl["runedancer_axe"].Template;

            var first = ClassTreeView.Next(Huscarl, allocation, "runedancer_axe")!.Value;
            Assert.That(first.Rank, Is.EqualTo(1));
            Assert.That(first.Line, Is.SameAs(axe.First));
            Assert.That(first.Strengthens, Is.False);

            allocation.Raise(Huscarl, "runedancer_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_axe")!.Value.Strengthens, Is.True, "rank 2 strengthens the first line");
            allocation.Raise(Huscarl, "runedancer_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_axe")!.Value.Line, Is.SameAs(axe.Second));
            allocation.Raise(Huscarl, "runedancer_axe");
            allocation.Raise(Huscarl, "runedancer_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_axe")!.Value.Line, Is.SameAs(axe.Third));
            allocation.Raise(Huscarl, "runedancer_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "runedancer_axe"), Is.Null, "nothing after rank 5");
        }
    }
}
