using System.Linq;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class HeroStatsViewTests
    {
        [SetUp]
        public void UseRussian()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void EveryLineHasATranslatedLabelAndSitsInAColumn()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking });

            var lines = HeroStatsView.Lines(loop, NumberFormatSettings.Default);

            Assert.That(lines.Count, Is.GreaterThan(20));
            foreach (var line in lines)
            {
                Assert.That(line.Label, Does.Not.StartWith("hero."), line.Label);
                Assert.That(line.Column, Is.InRange(0, HeroStatsView.Columns - 1));
                Assert.That(line.IsHeader || line.Value.Length > 0, Is.True, line.Label);
            }
            Assert.That(lines.Select(line => line.Label), Does.Contain("Шанс крита").And.Contain("Дерево и Путь").And.Contain("Сила"));
        }

        [Test]
        public void AverageHitWeighsCritsByChance()
        {
            Assert.That(HeroStatsView.AverageHit(100, 0.25, 3), Is.EqualTo(150).Within(1e-9));
            Assert.That(HeroStatsView.AverageHit(100, 0, 3), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void DamageShareCountsOnlyKeyboardDamage()
        {
            var modifiers = new[]
            {
                new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.3),
                new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.2, StatTags.Keyboard),
                new Modifier(CombatStats.Damage, ModifierKind.More, 0.5),
                new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.4, StatTags.Mouse),
                new Modifier(CombatStats.Gold, ModifierKind.Increased, 1),
            };

            var (increased, more) = HeroStatsView.DamageShare(modifiers);

            Assert.That(increased, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(more, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void TakenTreeNodesShowUpAsTheTreeSource()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking });
            var empty = loop.TreeModifiers.Count;
            loop.State.Tree.PointsEarned = 5;

            var node = loop.State.Tree.Allocatable(loop.Data.Tree).First(def => def.Type == Game.Core.Content.Tree.TreeNodeType.Small);
            Assert.That(loop.AllocateNode(node.Id), Is.EqualTo(Game.Core.Build.AllocationResult.Allocated));
            Assert.That(loop.TreeModifiers.Count, Is.GreaterThan(empty));
        }
    }
}
