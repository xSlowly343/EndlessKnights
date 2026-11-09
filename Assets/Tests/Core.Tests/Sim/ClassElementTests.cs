using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ClassElementTests
    {
        private static GameLoop Hero(HeroClass heroClass, StatSheet? hero = null)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(3) { Class = heroClass }, hero ?? new StatSheet());
            loop.Tick();
            return loop;
        }

        [Test]
        public void EveryClassHasAnElementAndEachElementHasThreeClasses()
        {
            var classes = System.Enum.GetValues(typeof(HeroClass)).Cast<HeroClass>().ToList();
            var elements = classes.Select(heroClass => TestWorld.Data.ClassTreeOf(heroClass.ToString().ToLowerInvariant())!.Element).ToList();

            Assert.That(elements, Has.None.EqualTo(ElementRules.None));
            for (var element = 0; element < ElementRules.Count; element++)
                Assert.That(elements.Count(e => e == element), Is.EqualTo(3), ElementRules.IdOf((Element)element));
            Assert.That(TestWorld.Data.ClassTreeOf("golemancer")!.Element, Is.EqualTo((int)Element.Earth));
        }

        [Test]
        public void ClassConversionGoesToTheClassElementOnly()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.ClassElementConversion, ModifierKind.Flat, 0.4));
            var loop = Hero(HeroClass.Golemancer, hero);

            loop.Combat.ElementMultiplier(StatTags.Strike);

            Assert.That(loop.Combat.ClassElement, Is.EqualTo((int)Element.Earth));
            Assert.That(loop.Combat.Portion(Element.Earth), Is.GreaterThan(0));
            Assert.That(loop.Combat.Portion(Element.Fire), Is.Zero);
        }

        [Test]
        public void ClassElementDamageAndPenOnlyHelpTheClassElement()
        {
            var plain = new StatSheet();
            plain.Add(new Modifier(CombatStats.ClassElementConversion, ModifierKind.Flat, 0.5));
            var strong = new StatSheet();
            strong.Add(new Modifier(CombatStats.ClassElementConversion, ModifierKind.Flat, 0.5));
            strong.Add(new Modifier(CombatStats.ClassElementDamage, ModifierKind.Flat, 0.3));
            strong.Add(new Modifier(CombatStats.ClassElementPen, ModifierKind.Flat, 0.2));
            var foreign = new StatSheet();
            foreign.Add(new Modifier(ElementRules.Conversion[(int)Element.Fire], ModifierKind.Flat, 0.5));
            foreign.Add(new Modifier(CombatStats.ClassElementDamage, ModifierKind.Flat, 0.3));

            var before = Hero(HeroClass.Golemancer, plain).Combat.ElementFactor(StatTags.Strike);
            var after = Hero(HeroClass.Golemancer, strong).Combat.ElementFactor(StatTags.Strike);
            var fire = Hero(HeroClass.Golemancer, foreign).Combat.ElementFactor(StatTags.Strike);

            Assert.That(after, Is.GreaterThanOrEqualTo(before + 0.5 * 0.3 - 1e-9));
            Assert.That(fire, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AShiftChangesTheClassElementAndTheStrongestShiftWins()
        {
            var path = new StatSheet();
            path.Add(new Modifier(ElementRules.Shift[(int)Element.Water], ModifierKind.Flat, 1));
            Assert.That(Hero(HeroClass.Golemancer, path).Combat.ClassElement, Is.EqualTo((int)Element.Water));

            var both = new StatSheet();
            both.Add(new Modifier(ElementRules.Shift[(int)Element.Water], ModifierKind.Flat, 1));
            both.Add(new Modifier(ElementRules.Shift[(int)Element.Fire], ModifierKind.Flat, 2));
            Assert.That(Hero(HeroClass.Golemancer, both).Combat.ClassElement, Is.EqualTo((int)Element.Fire));
        }

        [Test]
        public void EveryClassHasOnePathThatShiftsItsElement()
        {
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
            {
                var id = heroClass.ToString().ToLowerInvariant();
                var own = TestWorld.Data.ClassTreeOf(id)!.Element;
                var shifting = TestWorld.Data.Ascendancy.Subclasses
                    .Where(path => path.Id.StartsWith(id + "_"))
                    .Count(path => path.Trait != null && path.Trait.Levels[0].Any(line => ElementRules.Shift.Contains(line.Stat)));
                Assert.That(shifting, Is.EqualTo(1), id);
            }
        }

        [Test]
        public void SixUniquesShiftOneElementEach()
        {
            var uniques = TestWorld.Data.Items.Uniques.Where(unique => unique.Lines.Any(line => ElementRules.Shift.Contains(line.Stat))).ToList();

            Assert.That(uniques.Count, Is.EqualTo(ElementRules.Count));
            for (var element = 0; element < ElementRules.Count; element++)
                Assert.That(uniques.Count(unique => unique.Lines.Any(line => line.Stat == ElementRules.Shift[element])), Is.EqualTo(1));
        }
    }
}
