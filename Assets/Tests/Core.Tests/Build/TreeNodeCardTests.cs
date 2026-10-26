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
    public class TreeNodeCardTests
    {
        private static PassiveTree Tree => TestWorld.Data.Tree;

        private static GameLoop Ronin(int points)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
            loop.State.Tree.PointsEarned = points;
            return loop;
        }

        private static TreeNodeCard CardOf(GameLoop loop, string id) =>
            TreeNodeCard.ForPassive(Tree, loop.State.Tree, Tree[id], loop.RefundCost(id), NumberFormatSettings.Default, loop.Hero);

        [Test]
        public void CardHasNameKindStatsAndStatus()
        {
            var loop = Ronin(1);
            var node = Tree["ronin_ring_1"];

            var card = CardOf(loop, node.Id);

            Assert.That(card.Name, Is.EqualTo(node.NameRu));
            Assert.That(card.Kind, Is.EqualTo(TreeNodeText.TypeLabel(node)));
            Assert.That(card.Stats, Is.EqualTo(TreeNodeText.StatLines(loop.State.Tree, node)));
            Assert.That(card.Status, Is.EqualTo("Можно взять"));
        }

        [Test]
        public void DescriptionComesFromTheNodeOrFromItsType()
        {
            var loop = Ronin(1);

            Assert.That(CardOf(loop, "strength_keystone_3").Description, Does.StartWith("Таймер короче"));
            Assert.That(CardOf(loop, "ronin_ring_1").Description, Is.EqualTo("Прибавка на пути к значимым узлам."));
        }

        [Test]
        public void EveryKeystoneHasItsOwnDescriptionInBothLanguages()
        {
            var keystones = Tree.Nodes.Where(node => node.Type == TreeNodeType.Keystone).ToList();

            Assert.That(keystones, Is.Not.Empty);
            Assert.That(keystones.Where(node => node.DescriptionRu.Length == 0 || node.DescriptionEn.Length == 0).Select(node => node.Id), Is.Empty);
        }

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

        [Test]
        public void CardDeltasFollowTheHeroSheet()
        {
            var loop = Ronin(1);
            var node = Tree["ronin_ring_1"];

            var card = CardOf(loop, node.Id);

            Assert.That(card.Deltas, Has.Count.EqualTo(System.Math.Min(TreeNodeCard.MaxDeltas, node.Modifiers.Count)));
        }

        [Test]
        public void ShortenedCardKeepsOneStatAndDropsTheDescription()
        {
            var loop = Ronin(1);
            var card = CardOf(loop, "strength_keystone_3");

            var shortened = card.Shortened(1);

            Assert.That(shortened.Stats, Has.Count.EqualTo(1));
            Assert.That(shortened.Description, Is.Empty);
            Assert.That(shortened.Name, Is.EqualTo(card.Name));
        }
    }
}
