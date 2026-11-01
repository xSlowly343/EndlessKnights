using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class TreeNodeCardTests
    {
        [Test]
        public void IncreasedShowsTheBonusBeforeAndAfterTakingTheNode()
        {
            var hero = new StatSheet();
            var damage = new StatId("damage");
            hero.Add(new Modifier(damage, ModifierKind.Increased, 0.45));
            var node = new Modifier(damage, ModifierKind.Increased, 0.15);

            var offered = TreeNodeCard.DeltasOf(new[] { node }, false, hero).Single();

            Assert.That(offered.What, Is.EqualTo("Урон"));
            Assert.That(offered.From, Is.EqualTo("+45%"));
            Assert.That(offered.To, Is.EqualTo("+60%"));
        }

        [Test]
        public void TakenNodeShowsWhatItAlreadyAdds()
        {
            var hero = new StatSheet();
            var damage = new StatId("damage");
            var node = new Modifier(damage, ModifierKind.More, 0.25, StatTags.Keyboard);
            hero.Add(node);

            var taken = TreeNodeCard.DeltasOf(new[] { node }, true, hero).Single();

            Assert.That(taken.What, Is.EqualTo("Урон от клавиш"));
            Assert.That(taken.From, Is.EqualTo("×1.00"));
            Assert.That(taken.To, Is.EqualTo("×1.25"));
        }

        [Test]
        public void FlatUsesTheStatUnit()
        {
            var hero = new StatSheet();
            var timer = new StatId("bossTimerSeconds");

            var delta = TreeNodeCard.DeltasOf(new[] { new Modifier(timer, ModifierKind.Flat, -9) }, false, hero).Single();

            Assert.That(delta.From, Is.EqualTo("+0 с"));
            Assert.That(delta.To, Is.EqualTo("-9 с"));
        }

        [Test]
        public void DeltasStopAtThree()
        {
            var hero = new StatSheet();
            var modifiers = Enumerable.Range(0, 5).Select(i => new Modifier(new StatId("damage"), ModifierKind.Increased, 0.01 * (i + 1))).ToArray();

            Assert.That(TreeNodeCard.DeltasOf(modifiers, false, hero), Has.Count.EqualTo(TreeNodeCard.MaxDeltas));
        }
    }
}
