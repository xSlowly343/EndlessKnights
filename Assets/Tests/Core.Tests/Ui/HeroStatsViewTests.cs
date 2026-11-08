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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });

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
        public void TheSheetShowsTheAttackSourcesAndOneLinePerDamageGroup()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            loop.Hero.Add(new Modifier(CombatStats.AdditionalDamage, ModifierKind.Flat, 0.25));
            loop.RefreshClassAttribute();

            var lines = HeroStatsView.Lines(loop, NumberFormatSettings.Default);
            var labels = lines.Select(line => line.Label).ToList();

            Assert.That(labels, Does.Contain("Из чего АТК").And.Contain("Группы урона").And.Contain("Сила атаки %").And.Contain("Наносимый урон").And.Contain("Все группы"));
            Assert.That(lines.First(line => line.Label == "Доп. урон").Value, Is.EqualTo("×1.25"));
            Assert.That(labels, Does.Not.Contain("Темп"));
        }

        [Test]
        public void AFactorAboveAThousandIsShortened()
        {
            Assert.That(HeroStatsView.Factor(1.234), Is.EqualTo("×1.23"));
            Assert.That(HeroStatsView.Factor(2.5e6), Does.StartWith("×").And.Not.Contain("2500000"));
        }

        [Test]
        public void ThePreviewNamesOnlyTheGroupsThatMove()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            loop.Tick();
            var weapon = loop.Weapons.Give(TestWorld.Data.Weapons.Bases[0], Game.Core.Content.Items.ItemRarity.Legendary);

            var change = DamagePreview.Weapon(loop, weapon);
            var groups = DamagePreviewText.Groups(change);

            Assert.That(DamagePreviewText.Summary(change), Does.StartWith("Урон за удар ×"));
            Assert.That(groups.Select(line => line.Label), Does.Contain("Сила атаки").And.Not.Contain("Крит в среднем"));
            Assert.That(DamagePreviewText.Summary(DamageChange.None), Is.EqualTo("урон не меняется"));
        }

        [Test]
        public void TheMountRowsCoverEverySlotWithItsFacets()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            loop.State.Mount.Levels[0] = 6;

            var rows = MountView.Rows(loop);
            var facets = MountView.Facets(loop, 0);

            Assert.That(rows.Count, Is.EqualTo(Game.Core.Content.MountRules.Slots));
            Assert.That(rows[0].Name, Is.EqualTo("Оружие"));
            Assert.That(rows[0].Badge, Is.EqualTo("+6"));
            Assert.That(rows[1].Badge, Is.Empty);
            Assert.That(facets.Count, Is.EqualTo(5));
            Assert.That(facets[0].Open, Is.True);
            Assert.That(facets[1].Open, Is.False);
            Assert.That(facets[0].Options.Count, Is.EqualTo(3));
            Assert.That(MountView.OptionName(facets[0].Options[0]), Is.EqualTo("Остриё"));
            Assert.That(MountView.Summary(loop), Does.Contain("Скань").And.Contain("дальше +5%"));
        }

        [Test]
        public void AverageHitWeighsCritsByChance()
        {
            Assert.That(HeroStatsView.AverageHit(100, 0.25, 3), Is.EqualTo(150).Within(1e-9));
            Assert.That(HeroStatsView.AverageHit(100, 0, 3), Is.EqualTo(100).Within(1e-9));
            Assert.That(HeroStatsView.AverageHit(100, 1, 3, 0.5, 3), Is.EqualTo(100 * (1 + 2 + 0.5 * 3 * 2)).Within(1e-9));
        }

        [Test]
        public void DamageShareCountsOnlyKeyboardDamage()
        {
            var modifiers = new[]
            {
                new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.3),
                new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.2, StatTags.Strike),
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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            var empty = loop.TreeModifiers.Count;
            loop.State.TreePointsEarned = 5;

            Assert.That(loop.RaiseNode("runedancer_r1c0"), Is.EqualTo(Game.Core.Build.RankResult.Done));
            Assert.That(loop.TreeModifiers.Count, Is.GreaterThan(empty));
        }
    }
}
