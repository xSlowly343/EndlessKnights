using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassAttributeTests
    {
        private static GameLoop Hero(HeroClass heroClass, double attribute)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = heroClass, ClassChosen = true });
            loop.RebuildTree();
            if (attribute > 0)
                loop.Hero.Add(new Modifier(ClassAttributeDef.Stat, ModifierKind.Flat, attribute));
            loop.RefreshClassAttribute();
            return loop;
        }

        [Test]
        public void EveryClassTreeNamesItsAttributeAndItsFeature()
        {
            var known = typeof(CombatStats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(StatId))
                .Select(field => ((StatId)field.GetValue(null)!).Name)
                .ToHashSet();
            var classes = HeroClassNames.ByName.Keys.Concat(new[] { "spinner", "rimecaller", "thornmaiden", "clayshaper" }).ToList();
            Assert.That(classes.Count, Is.EqualTo(18));
            foreach (var heroClass in classes)
            {
                var tree = ClassTreeLoader.Load(TestWorld.Data.TreeTemplates, heroClass, TestData.ReadGameDataFile(ClassTreeLoader.FileNameOf(heroClass)));
                Assert.That(ClassAttributeDef.Names, Does.Contain(tree.Attribute.Name), tree.Id);
                Assert.That(tree.Attribute.PerPoint, Is.Not.Empty, tree.Id);
                Assert.That(tree.Attribute.PerPoint.Select(modifier => modifier.Stat.BaseName).Where(stat => !known.Contains(stat)), Is.Empty, tree.Id);
            }
        }

        [Test]
        public void TheClassAttributeStrengthensTheHuscarlShield()
        {
            var plain = Hero(HeroClass.Viking, 0);
            var strong = Hero(HeroClass.Viking, 20);

            Assert.That(strong.ClassAttribute, Is.EqualTo(20));
            var perPoint = strong.ClassAttributeDef.PerPoint[0].Value;
            Assert.That(strong.Hero.Value(CombatStats.ShieldDamage, 1, StatTags.Keyboard) - plain.Hero.Value(CombatStats.ShieldDamage, 1, StatTags.Keyboard),
                Is.EqualTo(20 * perPoint).Within(1e-9));
        }

        [Test]
        public void EachClassFeelsItsOwnFeature()
        {
            var amazon = Hero(HeroClass.Amazon, 10);

            Assert.That(amazon.Hero.Value(CombatStats.MarkPower, 0), Is.GreaterThan(0));
            Assert.That(amazon.Hero.Value(CombatStats.ShieldDamage, 1), Is.EqualTo(1), "the Huscarl shield stays out of the Huntress build");
        }

        [Test]
        public void TheFeatureFollowsTheAttributeWhenItChanges()
        {
            var loop = Hero(HeroClass.Viking, 0);
            var extra = new Modifier(ClassAttributeDef.Stat, ModifierKind.Flat, 10);
            loop.Hero.Add(extra);
            loop.Tick();
            var raised = loop.Hero.Value(CombatStats.ShieldDamage, 1, StatTags.Keyboard);
            loop.Hero.Remove(extra);
            loop.Tick();

            Assert.That(raised, Is.GreaterThan(1));
            Assert.That(loop.Hero.Value(CombatStats.ShieldDamage, 1, StatTags.Keyboard), Is.EqualTo(1).Within(1e-12));
            Assert.That(loop.ClassAttributeModifiers, Is.Empty);
        }

        [Test]
        public void ANoviceHasNoFeatureToStrengthen()
        {
            var loop = TestWorld.Loop(state: new GameState { ClassChosen = false });
            loop.Hero.Add(new Modifier(ClassAttributeDef.Stat, ModifierKind.Flat, 10));
            loop.RefreshClassAttribute();

            Assert.That(loop.ClassAttributeModifiers, Is.Empty);
        }

        [Test]
        public void TheTreeFileMustNameARealAttributeThatDoesNotFeedItself()
        {
            var json = TestData.ReadGameDataFile(ClassTreeLoader.FileNameOf("viking"));
            var templates = TestWorld.Data.TreeTemplates;

            Assert.Throws<ContentException>(() => ClassTreeLoader.Load(templates, "viking", json.Replace("\"name\": \"strength\"", "\"name\": \"luck\"")));
            Assert.Throws<ContentException>(() => ClassTreeLoader.Load(templates, "viking", json.Replace("\"perPoint\": [{\"stat\": \"shieldDamage\"", "\"perPoint\": [{\"stat\": \"classAttribute\"")));
        }
    }
}
