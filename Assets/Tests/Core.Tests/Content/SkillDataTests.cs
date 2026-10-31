using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class SkillDataTests
    {
        private const string Strike = "{ \"id\": \"x\", \"kind\": \"active\", \"weapon\": \"axe\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"tags\": [\"Keyboard\", \"Skill\"], \"cost\": { \"resource\": 10 }, \"cooldownSeconds\": 4, \"effect\": { \"type\": \"strike\", \"multiplier\": 4 }";

        private static string RealFile() => TestData.ReadGameDataFile(GemCatalog.FileName);

        private static GemCatalog WithExtra(string gemJson) => GemCatalog.Load(RealFile().Replace("\"gems\": [", "\"gems\": [\n    " + gemJson + ","));

        private static string Option(string id, string extra = "") => "{ \"id\": \"" + id + "\", \"name\": { \"ru\": \"" + id + "\", \"en\": \"" + id + "\" }" + extra + " }";

        private static string Tier(int level, string first, string second) => "{ \"level\": " + level + ", \"options\": [" + first + ", " + second + "] }";

        private static string ThreeTiers(string firstOption) =>
            "\"branches\": [" + Tier(5, firstOption, Option("b")) + ", " + Tier(15, Option("c"), Option("d")) + ", " + Tier(25, Option("e"), Option("f")) + "]";

        [Test]
        public void EverySkillHasAType()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.All.Count(gem => gem.Type == SkillType.Aura), Is.EqualTo(115));
            Assert.That(gems.All.Count(gem => gem.Type == SkillType.Support), Is.EqualTo(30));
            Assert.That(gems.All.Where(gem => gem.Type == SkillType.Aura && !gem.Locked).Select(gem => gem.Skill!.Effect), Is.All.InstanceOf<AuraEffect>());
            Assert.That(gems.All.Where(gem => gem.Type == SkillType.Active).Select(gem => gem.Skill!.Effect), Is.All.Not.InstanceOf<AuraEffect>());
            Assert.That(gems.All.Where(gem => gem.Type == SkillType.Support).Select(gem => gem.Supports), Is.All.EqualTo(StatTags.Skill));
            Assert.That(gems.All.Where(gem => gem.Type == SkillType.Support).Select(gem => gem.DamageMultiplier), Is.All.EqualTo(1));
        }

        [Test]
        public void AnAuraEffectNeedsTheAuraKind()
        {
            var json = "{ \"id\": \"x\", \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"durationSeconds\": 5, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }";

            var error = Assert.Throws<ContentException>(() => WithExtra(json))!;

            Assert.That(error.Message, Does.Contain("has kind aura"));
        }

        [Test]
        public void TheAuraKindNeedsAnAuraEffect()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike.Replace("\"active\"", "\"aura\"") + " }"))!;

            Assert.That(error.Message, Does.Contain("an aura needs an aura effect"));
        }

        [Test]
        public void BranchesGiveThreeTiersOfTwo()
        {
            var gem = WithExtra(Strike + ", " + ThreeTiers(Option("a", ", \"damage\": 1.5, \"cooldown\": 0.8, \"effect\": { \"multiplier\": 7, \"queueTargets\": 2 }")) + " }")["x"];

            Assert.That(gem.Branches.Select(tier => tier.Level), Is.EqualTo(new[] { 5, 15, 25 }));
            Assert.That(gem.Branches.Select(tier => tier.Options.Count), Is.All.EqualTo(2));
            var first = gem.Branches[0].Options[0];
            Assert.That(first.DamageMultiplier, Is.EqualTo(1.5));
            Assert.That(first.CooldownMultiplier, Is.EqualTo(0.8));
            Assert.That(first.CostMultiplier, Is.EqualTo(1));
            var strike = (StrikeEffect)first.Effect!;
            Assert.That(strike.Multiplier, Is.EqualTo(7));
            Assert.That(strike.QueueTargets, Is.EqualTo(2));
            Assert.That(gem.Branches[0].Options[1].Effect, Is.Null);
        }

        [Test]
        public void ASkillWithoutBranchesHasNone()
        {
            Assert.That(GemCatalog.Load(RealFile())["thin_cut"].Branches, Is.Empty);
        }

        [TestCase("\"branches\": []", "3 branch tiers, got 0")]
        [TestCase("\"branches\": [{ \"level\": 5, \"options\": [] }, { \"level\": 15, \"options\": [] }, { \"level\": 25, \"options\": [] }]", "2 branches, got 0")]
        [TestCase("\"branches\": [{ \"level\": 15, \"options\": [{ \"id\": \"a\", \"name\": { \"ru\": \"a\", \"en\": \"a\" } }, { \"id\": \"b\", \"name\": { \"ru\": \"b\", \"en\": \"b\" } }] }, { \"level\": 5, \"options\": [] }, { \"level\": 25, \"options\": [] }]", "must be above the previous tier level")]
        public void BrokenBranchesAreRejected(string branches, string message)
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", " + branches + " }"))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void ABranchIdIsUsedOncePerSkill()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", " + ThreeTiers(Option("c")) + " }"))!;

            Assert.That(error.Message, Does.Contain("branch id 'c' is used twice"));
        }

        [Test]
        public void ABranchKeepsTheEffectType()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", " + ThreeTiers(Option("a", ", \"effect\": { \"type\": \"flurry\" }")) + " }"))!;

            Assert.That(error.Message, Does.Contain("not its type"));
        }

        [Test]
        public void ABranchEffectIsCheckedLikeTheSkillEffect()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", " + ThreeTiers(Option("a", ", \"effect\": { \"multiplier\": -1 }")) + " }"))!;

            Assert.That(error.Message, Does.Contain("branches[0].options[0].effect.multiplier"));
        }

        [Test]
        public void ABranchMultiplierStaysInRange()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", " + ThreeTiers(Option("a", ", \"cost\": 0")) + " }"))!;

            Assert.That(error.Message, Does.Contain("cost"));
        }

        [Test]
        public void OnlySkillsAndAurasHaveBranches()
        {
            var support = "{ \"id\": \"x\", \"kind\": \"support\", \"supports\": [\"Skill\"], \"damageMult\": 1.2, \"manaMult\": 1.3, \"level\": 1, \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"effect\": { \"type\": \"chain\", \"share\": 0.3 }, \"branches\": [] }";

            var error = Assert.Throws<ContentException>(() => WithExtra(support))!;

            Assert.That(error.Message, Does.Contain("only active skills and auras have branches"));
        }

        [Test]
        public void ASupportNamesWhatItSupportsAndItsMultipliers()
        {
            var support = WithExtra("{ \"id\": \"x\", \"kind\": \"support\", \"supports\": [\"DoT\"], \"damageMult\": 1.2, \"manaMult\": 1.3, \"level\": 1, \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"effect\": { \"type\": \"chain\", \"share\": 0.3 } }");

            var gem = support["x"];
            Assert.That(gem.Supports, Is.EqualTo(StatTags.DoT));
            Assert.That(gem.DamageMultiplier, Is.EqualTo(1.2));
            Assert.That(gem.ManaMultiplier, Is.EqualTo(1.3));
            Assert.That(gem.CanSupport(support.All.First(skill => skill.Type == SkillType.Active && (skill.Tags & StatTags.DoT) != 0)), Is.True);
            Assert.That(gem.CanSupport(support["iron_rim_bash"]), Is.False);
            Assert.That(gem.CanSupport(support["chain"]), Is.False);
        }

        [TestCase("\"supports\": [\"Skill\"], \"damageMult\": 1", "manaMult")]
        [TestCase("\"supports\": [], \"damageMult\": 1, \"manaMult\": 1", "at least one skill tag")]
        public void ASupportWithoutItsFieldsIsRejected(string fields, string message)
        {
            var error = Assert.Throws<ContentException>(() => WithExtra("{ \"id\": \"x\", \"kind\": \"support\", " + fields + ", \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"effect\": { \"type\": \"chain\", \"share\": 0.3 } }"))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void OnlyASupportHasSupportFields()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", \"damageMult\": 2 }"))!;

            Assert.That(error.Message, Does.Contain("only a support has this field"));
        }

        [Test]
        public void ARemovedHuscarlSkillLeadsToTheSkillThatTookItAsABranch()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.TryGet("rivet", out var rivet), Is.True);
            Assert.That(rivet.Id, Is.EqualTo("iron_rim_bash"));
            Assert.That(rivet.Branches.SelectMany(tier => tier.Options).Select(option => option.Id), Does.Contain("rivet"));
            Assert.That(gems.TryGet("the_unfading_forge", out var forge) && forge.Type == SkillType.Aura, Is.True);
        }

        [Test]
        public void TheRosterNamesClassesWithoutFifteenSkillsAndFourAuras()
        {
            var gems = GemCatalog.Load(RealFile());

            var problems = SkillRoster.Problems(gems, new[] { "axe", "katana", "nothing" });

            Assert.That(problems.Where(problem => problem.StartsWith("axe")), Is.Empty, "the Huscarl is the reference class");
            Assert.That(problems, Has.Member("katana: 30 active skills, expected 15"));
            Assert.That(problems, Has.Member("nothing: 0 active skills, expected 15"));
            Assert.That(problems, Has.Member("nothing: 0 auras, expected 4"));
        }
    }
}
