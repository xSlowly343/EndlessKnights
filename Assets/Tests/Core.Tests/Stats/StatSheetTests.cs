using Game.Core.Math;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Stats
{
    public class StatSheetTests
    {
        private const double Tolerance = 1e-9;
        private static readonly StatId Damage = new StatId("damage");
        private static readonly StatId Gold = new StatId("gold");

        private static double Value(StatSheet sheet, StatId stat, double baseValue, StatTags tags = StatTags.None) =>
            sheet.Compute(stat, baseValue, tags).ToDouble();

        [Test]
        public void NoModifiersReturnsBase()
        {
            Assert.That(Value(new StatSheet(), Damage, 10), Is.EqualTo(10).Within(Tolerance));
        }

        [Test]
        public void TreeIncreasesAddUpInTheirOwnPoolAndMultiplyWithTheRest()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 1));
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.2, StatTags.None, ModifierPool.Tree));
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.3, StatTags.None, ModifierPool.Tree));

            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(10 * 2 * 1.5).Within(Tolerance));
            Assert.That(sheet.TotalsOf(Damage).IncreasedMultiplier, Is.EqualTo(3).Within(Tolerance));
        }

        [Test]
        public void TheTreeLayerOfTheGameLoopUsesTheTreePool()
        {
            var loop = Sim.TestWorld.Loop(state: new Game.Core.Sim.GameState { HeroLevel = 10 });
            loop.Levels.SyncTreePoints();
            loop.RaiseNode("viking_axe");

            Assert.That(loop.TreeModifiers, Is.Not.Empty);
            Assert.That(System.Linq.Enumerable.All(loop.TreeModifiers, modifier => modifier.Pool == ModifierPool.Tree), Is.True);
        }

        [Test]
        public void FormulaMatchesPathOfExile()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Flat, 5));
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.2));
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.3));
            sheet.Add(new Modifier(Damage, ModifierKind.More, 0.5));
            sheet.Add(new Modifier(Damage, ModifierKind.More, 0.2));

            Assert.That(Value(sheet, Damage, 10), Is.EqualTo((10 + 5) * (1 + 0.2 + 0.3) * (1.5 * 1.2)).Within(Tolerance));
        }

        [Test]
        public void IncreasedAddsUpButMoreMultiplies()
        {
            var increased = new StatSheet();
            increased.Add(new Modifier(Damage, ModifierKind.Increased, 0.5));
            increased.Add(new Modifier(Damage, ModifierKind.Increased, 0.5));
            var more = new StatSheet();
            more.Add(new Modifier(Damage, ModifierKind.More, 0.5));
            more.Add(new Modifier(Damage, ModifierKind.More, 0.5));

            Assert.That(Value(increased, Damage, 100), Is.EqualTo(200).Within(Tolerance));
            Assert.That(Value(more, Damage, 100), Is.EqualTo(225).Within(Tolerance));
        }

        [Test]
        public void NegativeModifiersReduce()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, -0.25));
            sheet.Add(new Modifier(Damage, ModifierKind.More, -0.2));

            Assert.That(Value(sheet, Damage, 100), Is.EqualTo(60).Within(Tolerance));
        }

        [Test]
        public void ModifiersOnlyAffectTheirStat()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Gold, ModifierKind.Increased, 1.0));

            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(10).Within(Tolerance));
            Assert.That(Value(sheet, Gold, 10), Is.EqualTo(20).Within(Tolerance));
        }

        [Test]
        public void TaggedModifierNeedsAllItsTags()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 1.0, StatTags.Keyboard));
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 1.0, StatTags.Keyboard | StatTags.Crit));

            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(10).Within(Tolerance));
            Assert.That(Value(sheet, Damage, 10, StatTags.Mouse), Is.EqualTo(10).Within(Tolerance));
            Assert.That(Value(sheet, Damage, 10, StatTags.Keyboard), Is.EqualTo(20).Within(Tolerance));
            Assert.That(Value(sheet, Damage, 10, StatTags.Keyboard | StatTags.Crit | StatTags.Skill), Is.EqualTo(30).Within(Tolerance));
        }

        [Test]
        public void UntaggedModifierAppliesEverywhere()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Flat, 1));

            Assert.That(Value(sheet, Damage, 1, StatTags.Mouse | StatTags.Skill), Is.EqualTo(2).Within(Tolerance));
        }

        [Test]
        public void WorksWithNumbersBeyondDoubleRange()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.More, 1.0));

            var result = sheet.Compute(Damage, BigNumber.Create(1.5, 400));

            Assert.That(result, Is.EqualTo(BigNumber.Create(3.0, 400)));
        }

        [Test]
        public void RepeatedReadsUseTheCache()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.5));

            for (var i = 0; i < 100; i++)
                Value(sheet, Damage, 10, StatTags.Keyboard);

            Assert.That(sheet.RecalculationCount, Is.EqualTo(1));
        }

        [Test]
        public void ChangingTheBuildInvalidatesTheCache()
        {
            var sheet = new StatSheet();
            var modifier = new Modifier(Damage, ModifierKind.Increased, 0.5);
            sheet.Add(modifier);
            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(15).Within(Tolerance));

            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.5));
            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(20).Within(Tolerance));

            Assert.That(sheet.Remove(modifier), Is.True);
            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(15).Within(Tolerance));

            sheet.Clear();
            Assert.That(Value(sheet, Damage, 10), Is.EqualTo(10).Within(Tolerance));
            Assert.That(sheet.RecalculationCount, Is.EqualTo(4));
        }

        [Test]
        public void DifferentTagQueriesAreCachedSeparately()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 1.0, StatTags.Mouse));

            Assert.That(Value(sheet, Damage, 10, StatTags.Mouse), Is.EqualTo(20).Within(Tolerance));
            Assert.That(Value(sheet, Damage, 10, StatTags.Keyboard), Is.EqualTo(10).Within(Tolerance));
            Assert.That(sheet.RecalculationCount, Is.EqualTo(2));
        }

        [Test]
        public void StatIdsCompareByName()
        {
            Assert.That(new StatId("damage"), Is.EqualTo(Damage));
            Assert.That(new StatId("damage") == Damage, Is.True);
            Assert.That(new StatId("Damage") != Damage, Is.True);
        }
    }
}
