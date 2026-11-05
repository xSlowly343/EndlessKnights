using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class SkillDataTests
    {
        private const string Strike = "{ \"id\": \"x\", \"kind\": \"active\", \"weapon\": \"rune_daggers\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"tags\": [\"Strike\", \"Skill\"], \"cost\": { \"resource\": 10 }, \"cooldownSeconds\": 4, \"effect\": { \"type\": \"strike\", \"multiplier\": 4 }";

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

            Assert.That(gems.All.Count(gem => gem.Type == SkillType.Aura), Is.EqualTo(118));
            Assert.That(gems.All.Count(gem => gem.Type == SkillType.Support), Is.EqualTo(84));
            Assert.That(gems.All.Where(gem => gem.Type == SkillType.Aura && !gem.Locked).Select(gem => gem.Skill!.Effect), Is.All.InstanceOf<AuraEffect>());
            Assert.That(gems.All.Where(gem => gem.Type == SkillType.Active).Select(gem => gem.Skill!.Effect), Is.All.Not.InstanceOf<AuraEffect>());
            var supports = gems.All.Where(gem => gem.Type == SkillType.Support).ToList();
            Assert.That(supports.Where(gem => gem.ForSkill == null).Select(gem => gem.Fits), Is.All.Not.EqualTo(SkillMechanic.None));
            Assert.That(supports.Select(gem => gem.ManaMultiplier), Is.All.InRange(1.0, 1.1));
            Assert.That(supports.Where(gem => gem.ManaMultiplier > 1).Select(gem => gem.Id), Is.EquivalentTo(new[] { "chain", "twins", "double_charge", "wide_reach", "carrier_boss", "carrier_full", "carrier_keys", "carrier_mouse", "carrier_low" }));
            Assert.That(supports.Select(gem => gem.Id), Has.None.AnyOf("lingering", "long_swing", "minion_trail"));
            Assert.That(supports.Any(gem => gem.DamageMultiplier != 1), Is.True, "not every support keeps the plain damage");
        }

        [Test]
        public void AnAuraEffectNeedsTheAuraKind()
        {
            var json = "{ \"id\": \"x\", \"level\": 1, \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"durationSeconds\": 5, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }";

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
            Assert.That(GemCatalog.Load(RealFile())["runedancer_path_1"].Branches, Is.Empty);
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
            var support = WithExtra("{ \"id\": \"x\", \"kind\": \"support\", \"fits\": [\"dot\"], \"damageMult\": 1.2, \"manaMult\": 1.3, \"level\": 1, \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"effect\": { \"type\": \"chain\", \"share\": 0.3 } }");

            var gem = support["x"];
            Assert.That(gem.Fits, Is.EqualTo(SkillMechanic.Dot));
            Assert.That(gem.DamageMultiplier, Is.EqualTo(1.2));
            Assert.That(gem.ManaMultiplier, Is.EqualTo(1.3));
            Assert.That(gem.CanSupport(support["runedancer_hit_1"]), Is.False);
            Assert.That(gem.CanSupport(support["chain"]), Is.False);
        }

        [TestCase("\"fits\": [\"strike\"], \"damageMult\": 1", "manaMult")]
        [TestCase("\"fits\": [], \"damageMult\": 1, \"manaMult\": 1", "at least one skill mechanic")]
        public void ASupportWithoutItsFieldsIsRejected(string fields, string message)
        {
            var error = Assert.Throws<ContentException>(() => WithExtra("{ \"id\": \"x\", \"kind\": \"support\", " + fields + ", \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"effect\": { \"type\": \"chain\", \"share\": 0.3 } }"))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void AClassAuraMustReserveResource()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra("{ \"id\": \"x\", \"kind\": \"aura\", \"weapon\": \"rune_daggers\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"durationSeconds\": 5, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }"))!;

            Assert.That(error.Message, Does.Contain("a class aura reserves resource"));
        }

        [Test]
        public void OnlyASupportHasSupportFields()
        {
            var error = Assert.Throws<ContentException>(() => WithExtra(Strike + ", \"damageMult\": 2 }"))!;

            Assert.That(error.Message, Does.Contain("only a support has this field"));
        }

        [Test]
        public void TheRosterNamesClassesWithoutTwelveSkillsAndSixAuras()
        {
            var gems = GemCatalog.Load(RealFile());

            var problems = SkillRoster.Problems(gems, TestWorld.Real.Weapons.Types.Select(type => type.Id).Append("nothing"));

            Assert.That(problems.Where(problem => !problem.StartsWith("nothing")), Is.Empty, "every class has twelve skills and six auras");
            Assert.That(problems, Has.Member("nothing: 0 active skills, expected 12"));
            Assert.That(problems, Has.Member("nothing: 0 auras, expected 6"));
        }

        [Test]
        public void EveryClassSkillHasThreeBranchTiersOfTwoOptions()
        {
            var gems = GemCatalog.Load(RealFile());
            var weapons = TestWorld.Real.Weapons.Types.Select(type => type.Id).ToList();

            Assert.That(SkillRoster.Problems(gems, weapons), Is.Empty);
            Assert.That(weapons, Has.Count.EqualTo(18));
        }

        [TestCase("runedancer")]
        [TestCase("duelist")]
        [TestCase("windshot")]
        [TestCase("shardcaller")]
        [TestCase("sandwalker")]
        public void ARewrittenCharacterHasANumberAndARuleInEveryTier(string character)
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(SkillRoster.NumbersOnlyTiers(gems).Where(tier => tier.StartsWith(character + "_", System.StringComparison.Ordinal)), Is.Empty);
            foreach (var gem in gems.All.Where(gem => gem.Id.StartsWith(character + "_", System.StringComparison.Ordinal) && gem.Skill?.Effect is not AuraEffect && gem.Branches.Count > 0))
            {
                foreach (var tier in gem.Branches)
                    Assert.That(tier.Options.Count(option => option.Effect == null), Is.EqualTo(1), gem.Id + " " + tier.Level);
            }
        }

        [Test]
        public void AClassSkillWithoutBranchesIsNamed()
        {
            var gems = GemCatalog.Load(RealFile());
            var json = RealFile().Replace("\r\n", "\n");
            var start = json.IndexOf("\"id\": \"runedancer_hit_1\"", System.StringComparison.Ordinal);
            var branches = json.IndexOf(",\n      \"branches\": [", start, System.StringComparison.Ordinal);
            var end = json.IndexOf("\n      ]", branches, System.StringComparison.Ordinal) + "\n      ]".Length;
            Assert.That(branches, Is.GreaterThan(start));

            var problems = SkillRoster.Problems(GemCatalog.Load(json.Substring(0, branches) + json.Substring(end)), new[] { "rune_daggers" });

            Assert.That(problems, Has.Member("runedancer_hit_1: 0 branch tiers, expected 3"));
            Assert.That(SkillRoster.Problems(gems, new[] { "rune_daggers" }), Is.Empty);
        }

        [Test]
        public void EveryPathHasExactlyOneSkill()
        {
            Assert.DoesNotThrow(() => TestWorld.Real.Ascendancy.RequirePathSkills(TestWorld.Real.Gems, TestWorld.Real.Weapons));
            Assert.That(TestWorld.Real.Ascendancy.Subclasses.Count(path => TestWorld.Real.Gems.All.Count(gem => gem.Ascendancy == path.Id) == 1), Is.EqualTo(54));
        }

        [Test]
        public void TheGameDoesNotLoadAClassWithoutTwelveSkills()
        {
            var error = Assert.Throws<ContentException>(() => GameData.Load(name =>
                name == GemCatalog.FileName
                    ? TestData.ReadGameDataFile(name).Replace("\r\n", "\n").Replace("\"id\": \"duelist_auto_3\",\n      \"kind\": \"active\",\n      \"weapon\": \"spectral_rapier\"", "\"id\": \"duelist_auto_3\",\n      \"kind\": \"active\",\n      \"weapon\": \"rune_daggers\"")
                    : TestData.ReadGameDataFile(name)));

            Assert.That(error!.Message, Does.Contain("spectral_rapier: 11 active skills, expected 12"));
            Assert.That(error.Message, Does.Contain("rune_daggers: 13 active skills, expected 12"));
        }
    }
}
