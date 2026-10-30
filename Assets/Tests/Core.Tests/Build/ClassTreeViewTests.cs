using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassTreeViewTests
    {
        private static readonly ClassTree Huscarl = ClassTreeLoader.Load(
            TreeTemplates.Load(TestData.ReadGameDataFile(TreeTemplates.FileName)), "viking", TestData.ReadGameDataFile(ClassTreeLoader.FileNameOf("viking")));

        [Test]
        public void HoveringANodeLightsItsParentsAndEveryNodeThatGrowsFromIt()
        {
            var chain = ClassTreeView.Chain(Huscarl, "viking_heavy_swing");

            Assert.That(chain, Is.EquivalentTo(new[] { "viking_axe", "viking_heavy_swing", "viking_long_streak", "viking_jarl_axe", "viking_north_wind" }));
            Assert.That(ClassTreeView.Chain(Huscarl, "viking_north_wind"), Is.EquivalentTo(new[] { "viking_axe", "viking_heavy_swing", "viking_jarl_axe", "viking_north_wind" }),
                "from the top only the way down is lit, not the side branches of the parents");
            Assert.That(ClassTreeView.Chain(Huscarl, "viking_nope"), Is.Empty);
        }

        [Test]
        public void ThePreviewShowsWhatTheNextRankAdds()
        {
            var allocation = new ClassTreeAllocation("viking") { PointsEarned = 10 };
            var axe = Huscarl["viking_axe"].Template;

            var first = ClassTreeView.Next(Huscarl, allocation, "viking_axe")!.Value;
            Assert.That(first.Rank, Is.EqualTo(1));
            Assert.That(first.Line, Is.SameAs(axe.First));
            Assert.That(first.Strengthens, Is.False);

            allocation.Raise(Huscarl, "viking_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "viking_axe")!.Value.Strengthens, Is.True, "rank 2 strengthens the first line");
            allocation.Raise(Huscarl, "viking_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "viking_axe")!.Value.Line, Is.SameAs(axe.Second));
            allocation.Raise(Huscarl, "viking_axe");
            allocation.Raise(Huscarl, "viking_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "viking_axe")!.Value.Line, Is.SameAs(axe.Third));
            allocation.Raise(Huscarl, "viking_axe");
            Assert.That(ClassTreeView.Next(Huscarl, allocation, "viking_axe"), Is.Null, "nothing after rank 5");
        }
    }
}
