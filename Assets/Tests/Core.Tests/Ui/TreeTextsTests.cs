using System.Linq;
using Game.Core.Content.Tree;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class TreeTextsTests
    {
        [Test]
        public void PointsAndTakenReadNaturally()
        {
            Texts.UseLanguage("ru");

            Assert.That(TreeTexts.Points(4), Is.EqualTo("Очков: 4"));
            Assert.That(TreeTexts.Taken(31, 1672), Is.EqualTo("взято 31 / 1672"));
            Assert.That(TreeTexts.Hint, Does.StartWith("Нажми на узел"));
        }

        [Test]
        public void ClassStartsAreNotCountedAsTakeable()
        {
            var tree = TestWorld.Real.Tree;
            var starts = tree.Nodes.Count(node => node.Type == TreeNodeType.Start);

            Assert.That(starts, Is.GreaterThan(0));
            Assert.That(TreeTexts.TakeableCount(tree), Is.EqualTo(tree.Nodes.Count - starts));
        }
    }
}
