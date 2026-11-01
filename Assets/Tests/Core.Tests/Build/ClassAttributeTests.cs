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
            var classes = HeroClassNames.ByName.Keys.ToList();
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
        public void ANoviceHasNoFeatureToStrengthen()
        {
            var loop = TestWorld.Loop(state: new GameState { ClassChosen = false });
            loop.Hero.Add(new Modifier(ClassAttributeDef.Stat, ModifierKind.Flat, 10));
            loop.RefreshClassAttribute();

            Assert.That(loop.ClassAttributeModifiers, Is.Empty);
        }
    }
}
