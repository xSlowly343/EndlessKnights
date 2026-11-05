using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class ChoiceScreenTests
    {
        private static GameData Data => TestWorld.Data;

        [Test]
        public void EveryCharacterHasAScreenWithRoleRadarPartnersAndThreePaths()
        {
            var cards = CharacterChoiceView.All(Data);

            Assert.That(cards.Select(card => card.Id), Is.EquivalentTo(HeroClassNames.ByName.Keys));
            foreach (var card in cards)
            {
                Assert.That(card.About, Is.Not.Empty, card.Id);
                Assert.That(card.Role, Does.Not.StartWith("party.role."), card.Id);
                Assert.That(card.Radar, Has.Count.EqualTo(CharacterCatalog.Axes), card.Id);
                Assert.That(card.Radar.All(point => point.Value >= 1 && point.Value <= 5), Is.True, card.Id);
                Assert.That(card.Partners.Count, Is.InRange(2, 3), card.Id);
                Assert.That(card.Paths, Has.Count.EqualTo(3), card.Id);
            }
        }

        [Test]
        public void TheRolesFollowTheRoster()
        {
            Assert.That(Data.Characters.TryGet("alchemist", out var alchemist), Is.True);
            Assert.That(alchemist.Role, Is.EqualTo("breaker"));
            Assert.That(Data.Characters.All.Select(c => c.Role), Is.EquivalentTo(HeroClassNames.ByName.Values.Select(heroClass => Data.Party.Search.RoleOf(heroClass))));
        }

        [TestCase("\"partners\": [\"minstrel\", \"whisperer\"]", "\"partners\": [\"runedancer\", \"whisperer\"]", "partner 'runedancer' must be another character")]
        [TestCase("\"role\": \"striker\", \"about\"", "\"role\": \"tank\", \"about\"", "role 'tank'")]
        [TestCase("\"radar\": [5, 3, 4, 2, 2, 3]", "\"radar\": [5, 3, 4, 2, 2]", "the radar has 6 values")]
        public void BrokenCharactersAreReported(string original, string broken, string message)
        {
            var json = TestData.ReadGameDataFile(CharacterCatalog.FileName);
            Assert.That(json, Does.Contain(original));

            var error = Assert.Throws<ContentException>(() => CharacterCatalog.Load(json.Replace(original, broken)).RequireRoster(Data.Party.Search.Roles))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void EveryPathReadsLikeAHeroTrait()
        {
            var loop = TestWorld.Loop();
            foreach (var path in Data.Ascendancy.Subclasses)
            {
                var card = PathTraitView.Build(loop, path, TraitDef.MaxLevel);
                Assert.That(card.Mechanic, Is.Not.Empty, path.Id);
                Assert.That(card.Skill, Is.Not.Empty, path.Id);
                Assert.That(card.Stats, Is.Not.Empty, path.Id);
                Assert.That(card.Rule, Is.Not.Empty, "the level 5 rule, like Artificial Moon in TLI: " + path.Id);
                Assert.That(card.Slots.Select(slot => slot.HeroLevel), Is.EqualTo(new[] { 45, 60, 75 }), path.Id);
                Assert.That(card.Slots.All(slot => slot.Options.Count == 2 && slot.Options.All(option => option.Name.Length > 0 && option.Text.Length > 0)), Is.True, path.Id);
            }
        }

        [Test]
        public void TheShownLevelAddsTheLevelsUpAndOnlyLevelFiveShowsTheRule()
        {
            var loop = TestWorld.Loop();
            var path = Data.Ascendancy.SubclassesOf("runedancer")[0];

            var first = PathTraitView.Build(loop, path, 1);
            var fourth = PathTraitView.Build(loop, path, 4);
            var totals = PathTraitView.Totals(path.Trait!, 4);

            Assert.That(first.RuleShown, Is.False);
            Assert.That(PathTraitView.Build(loop, path, 5).RuleShown, Is.True);
            Assert.That(fourth.Stats, Is.Not.EqualTo(first.Stats));
            Assert.That(totals.First(m => m.Stat.Name == "runeWord").Value, Is.EqualTo(16).Within(1e-9), "10 + 2 + 2 + 2");
        }

        [Test]
        public void ForksCanBePickedOnlyOnTheChosenPathAtTheirLevelAndOnce()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = 50 });
            loop.CompleteActivity("garden");
            var path = Data.Ascendancy.SubclassesOf("runedancer")[0];
            var other = Data.Ascendancy.SubclassesOf("runedancer")[1];
            loop.ChooseSubclass(path.Id);

            var card = PathTraitView.Build(loop, path, 1);
            Assert.That(card.Chosen, Is.True);
            Assert.That(card.Level, Is.EqualTo(1));
            Assert.That(card.Slots[0].Options.All(option => option.CanPick), Is.True, "level 45 is open at hero level 50");
            Assert.That(card.Slots[1].Options.Any(option => option.CanPick), Is.False, "level 60 is still closed");
            Assert.That(PathTraitView.Build(loop, other, 1).Slots[0].Options.Any(option => option.CanPick), Is.False, "another Path is only shown");

            loop.PickTraitFork(card.Slots[0].Options[1].Id);
            var after = PathTraitView.Build(loop, path, 1);
            Assert.That(after.Slots[0].Options[1].Picked, Is.True);
            Assert.That(after.Slots[0].Options.Any(option => option.CanPick), Is.False);
            Assert.That(after.Mode, Is.Not.Empty);
            Assert.That(after.Slots[0].Scale, Is.EqualTo(1));
            Assert.That(PathTraitView.Build(loop, path, 5).Slots[0].Scale, Is.EqualTo(Data.Ascendancy.ForkScaleAt(5)).And.GreaterThan(1));
            Assert.That(PathTraitView.Build(loop, path, 5).Slots[0].ScaleText, Is.Not.Empty);
        }
    }
}
