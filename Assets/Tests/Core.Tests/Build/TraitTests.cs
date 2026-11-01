using System.Linq;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class TraitTests
    {
        private const string Trait = @"""trait"": {
            ""levels"": [
                { ""modifiers"": [{ ""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.01 }] },
                { ""modifiers"": [{ ""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.02 }] },
                { ""modifiers"": [{ ""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.03 }] },
                { ""modifiers"": [{ ""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.04 }] },
                { ""modifiers"": [{ ""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.05 }] }
            ],
            ""party"": [{ ""stat"": ""partyDamage"", ""kind"": ""flat"", ""value"": 0.12 }],
            ""forks"": [
                { ""level"": 45, ""options"": [
                    { ""id"": ""long_word"", ""name"": { ""ru"": ""Длинное слово"", ""en"": ""Long Word"" }, ""modifiers"": [{ ""stat"": ""critChance"", ""kind"": ""flat"", ""value"": 0.1 }] },
                    { ""id"": ""rhyme"", ""name"": { ""ru"": ""Рифма"", ""en"": ""Rhyme"" }, ""modifiers"": [{ ""stat"": ""critChance"", ""kind"": ""flat"", ""value"": 0.2 }] } ] },
                { ""level"": 60, ""options"": [
                    { ""id"": ""rune_name"", ""name"": { ""ru"": ""Имя руны"", ""en"": ""Rune Name"" }, ""modifiers"": [{ ""stat"": ""critMultiplier"", ""kind"": ""flat"", ""value"": 0.5 }] },
                    { ""id"": ""quiet_rune"", ""name"": { ""ru"": ""Тихая руна"", ""en"": ""Quiet Rune"" }, ""modifiers"": [{ ""stat"": ""critMultiplier"", ""kind"": ""flat"", ""value"": 1 }] } ] }
            ]
        },";

        private static string Activities() =>
            string.Join(", ", Enumerable.Range(1, 8).Select(i => @"{ ""id"": ""a" + i + @""", ""name"": { ""ru"": ""А"", ""en"": ""A"" } }"));

        private static string Path(string id, string trait) =>
            @"{ ""id"": """ + id + @""", ""class"": ""runedancer"", ""name"": { ""ru"": ""П"", ""en"": ""P"" }, " + trait + @"
              ""nodes"": [{ ""id"": """ + id + @"_start"", ""type"": ""start"", ""x"": 0, ""y"": 0, ""name"": { ""ru"": ""П"", ""en"": ""P"" } }], ""links"": [] }";

        private static AscendancyCatalog Catalog(string trait = Trait) => AscendancyCatalog.Load(@"{
  ""version"": 1,
  ""activities"": [" + Activities() + @"],
  ""change"": { ""freeEveryHours"": 24, ""crystalCost"": 100, ""freeClassChanges"": 1 },
  ""subclasses"": [" + Path("berserker", trait) + ", " + Path("plain", "") + @"]
}");

        private static AscendancyState Chosen(AscendancyCatalog catalog, int activities, string path = "berserker")
        {
            var state = new AscendancyState();
            foreach (var activity in catalog.Activities.Take(activities))
                state.Complete(activity.Id);
            Assert.That(state.Choose(catalog, "runedancer", path), Is.EqualTo(SubclassResult.Chosen));
            return state;
        }

        private static double Sum(AscendancyState state, AscendancyCatalog catalog, StatId stat) =>
            state.Modifiers(catalog).Where(m => m.Stat == stat).Sum(m => m.Value);

        private static double LevelDamage(AscendancyState state, AscendancyCatalog catalog) =>
            state.Modifiers(catalog).Where(m => m.Stat == CombatStats.Damage && m.Kind == ModifierKind.Increased && m.Tags == StatTags.None).Sum(m => m.Value);

        [Test]
        public void TheTraitGrowsOneLevelPerPointUpToFive()
        {
            var catalog = Catalog();

            Assert.That(Chosen(catalog, 1).TraitLevel(catalog), Is.EqualTo(1));
            Assert.That(LevelDamage(Chosen(catalog, 2), catalog), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(Chosen(catalog, 8).TraitLevel(catalog), Is.EqualTo(TraitDef.MaxLevel));
            Assert.That(LevelDamage(Chosen(catalog, 8), catalog), Is.EqualTo(0.15).Within(1e-9));
        }

        [Test]
        public void ThePartyShareGoesToTheHeroSheetAndFromThereToAllies()
        {
            var catalog = Catalog();

            Assert.That(Sum(Chosen(catalog, 1), catalog, CombatStats.PartyDamage), Is.EqualTo(0.12).Within(1e-9));
        }

        [Test]
        public void AForkOpensAtItsHeroLevelAndTakesOneOfTwo()
        {
            var catalog = Catalog();
            var state = Chosen(catalog, 3);

            Assert.That(state.PickFork(catalog, 44, "long_word"), Is.EqualTo(TraitForkResult.HeroLevelTooLow));
            Assert.That(state.PickFork(catalog, 45, "long_word"), Is.EqualTo(TraitForkResult.Picked));
            Assert.That(state.PickFork(catalog, 45, "rhyme"), Is.EqualTo(TraitForkResult.AlreadyPicked));
            Assert.That(state.PickFork(catalog, 60, "quiet_rune"), Is.EqualTo(TraitForkResult.Picked));
            Assert.That(state.PickFork(catalog, 60, "nothing"), Is.EqualTo(TraitForkResult.UnknownOption));
            Assert.That(Sum(state, catalog, CombatStats.CritChance), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(Sum(state, catalog, CombatStats.CritMultiplier), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void ChangingTheTraitClearsTheForks()
        {
            var catalog = Catalog();
            var state = Chosen(catalog, 3);
            state.PickFork(catalog, 75, "rhyme");

            Assert.That(state.ChangePaid(catalog, "runedancer", "berserker"), Is.EqualTo(SubclassResult.Chosen));

            Assert.That(state.ForkPicks, Is.Empty);
            Assert.That(state.PickFork(catalog, 75, "long_word"), Is.EqualTo(TraitForkResult.Picked));
        }

        [Test]
        public void APathWithoutATraitHasNoForks()
        {
            var catalog = Catalog();
            var state = Chosen(catalog, 3, "plain");

            Assert.That(state.TraitLevel(catalog), Is.Zero);
            Assert.That(state.PickFork(catalog, 75, "long_word"), Is.EqualTo(TraitForkResult.NoTrait));
        }

        [Test]
        public void ForkPicksSurviveARestore()
        {
            var catalog = Catalog();
            var state = Chosen(catalog, 3);
            state.PickFork(catalog, 45, "rhyme");

            var restored = new AscendancyState();
            restored.Restore(state.Subclass, state.Allocated, state.CompletedActivities, state.ForkPicks);

            Assert.That(restored.Picked("rhyme"), Is.True);
            Assert.That(Sum(restored, catalog, CombatStats.CritChance), Is.EqualTo(0.2).Within(1e-9));
        }

        [TestCase(@"""levels"": [", @"""levels"": [{ ""modifiers"": [] },", "a trait has exactly 5 levels, got 6")]
        [TestCase(@"""level"": 60", @"""level"": 40", "fork levels must go up")]
        [TestCase(@"""id"": ""rhyme""", @"""id"": ""long_word""", "fork option id 'long_word' is used twice")]
        public void BrokenTraitsAreReported(string original, string broken, string message)
        {
            var error = Assert.Throws<ContentException>(() => Catalog(Trait.Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void EveryCharacterHasThreeTraitsWithForksAt45And60And75()
        {
            var catalog = TestWorld.Data.Ascendancy;

            foreach (var heroClass in HeroClassNames.ByName.Keys.Where(name => name != HeroClassNames.Novice))
            {
                var paths = catalog.SubclassesOf(heroClass);
                Assert.That(paths.Count, Is.EqualTo(3), heroClass);
                foreach (var path in paths)
                {
                    Assert.That(path.Trait, Is.Not.Null, path.Id);
                    Assert.That(path.Trait!.Forks.Select(fork => fork.HeroLevel), Is.EqualTo(new[] { 45, 60, 75 }), path.Id);
                    Assert.That(path.Trait.Party, Is.Not.Empty, path.Id);
                }
            }
        }
    }
}
