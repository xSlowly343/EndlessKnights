using System.Linq;
using Game.Core.Build;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BuildCodeTests
    {
        private static GameLoop Hero(string heroClass, int points)
        {
            var state = new GameState(1);
            var loop = TestWorld.Loop(new FakeInputSource(), state);
            loop.Tick();
            state.Class = HeroClassNames.ByName[heroClass];
            state.ClassChosen = true;
            state.TreePointsEarned = points;
            state.ClassTree = new ClassTreeAllocation(heroClass) { PointsEarned = points };
            return loop;
        }

        private static string FirstClass => TestWorld.Data.ClassTrees.Keys.OrderBy(id => id, System.StringComparer.Ordinal).First(id => HeroClassNames.ByName.ContainsKey(id));

        private static void SpendSome(GameLoop loop)
        {
            var tree = loop.ClassTreeDef!;
            foreach (var node in tree.Nodes.Where(n => n.Row == 1).Take(3))
                for (var i = 0; i < 3; i++)
                    Assert.That(loop.State.ClassTree.Raise(tree, node.Id), Is.EqualTo(RankResult.Done));
        }

        [Test]
        public void ABuildSurvivesTheCodeAndFitsADiscordMessage()
        {
            var loop = Hero(FirstClass, 40);
            SpendSome(loop);

            var code = loop.ShareBuild();
            var build = loop.ReadBuild("  " + code + "\n");

            Assert.That(code, Does.StartWith(BuildCode.Prefix));
            Assert.That(code.Length, Is.LessThan(BuildCode.MaxLength));
            Assert.That(code, Does.Match("^[A-Za-z0-9_-]+$"));
            Assert.That(build, Is.Not.Null);
            Assert.That(build!.TreeId, Is.EqualTo(FirstClass));
            Assert.That(build.Points, Is.EqualTo(9));
            Assert.That(build.Skills.Select(s => s[0]), Is.EqualTo(loop.State.Skills.Select(s => s.Skill.Id)));
        }

        [Test]
        public void AnotherHeroOfTheSameClassFollowsTheSharedPlan()
        {
            var author = Hero(FirstClass, 40);
            SpendSome(author);
            var code = author.ShareBuild();
            var reader = Hero(FirstClass, 5);

            Assert.That(reader.ImportBuild(code), Is.EqualTo(BuildImportResult.Done));

            Assert.That(reader.State.ClassTree.PointsSpent, Is.EqualTo(5), "the free points go by the plan at once");
            Assert.That(reader.State.ClassTree.Plan.Count, Is.EqualTo(9));
            reader.State.TreePointsEarned = 40;
            reader.State.ClassTree.PointsEarned = 40;
            reader.FillTrees();
            Assert.That(reader.State.ClassTree.Ranks, Is.EquivalentTo(author.State.ClassTree.Ranks));
        }

        [Test]
        public void AResetTreeIsRebuiltByTheSharedCode()
        {
            var author = Hero(FirstClass, 40);
            var tree = author.ClassTreeDef!;
            SpendSome(author);
            var slot = tree.Majors[0];
            author.State.ClassTree.RestoreMajor(slot.At, slot.Options[slot.Options.Count - 1].Id);
            var code = author.ShareBuild();
            var reader = Hero(FirstClass, 40);
            foreach (var node in tree.Nodes.Where(n => n.Row == 1).Skip(3).Take(2))
                for (var i = 0; i < 5; i++)
                    Assert.That(reader.State.ClassTree.Raise(tree, node.Id), Is.EqualTo(RankResult.Done));
            reader.State.ClassTree.PointsEarned = 10;
            reader.State.TreePointsEarned = 10;
            reader.State.Gold = Game.Core.Math.BigNumber.FromDouble(1e30);

            Assert.That(reader.ImportBuild(code), Is.EqualTo(BuildImportResult.Done));
            Assert.That(reader.State.ClassTree.PointsSpent, Is.EqualTo(10), "nothing is taken back by the code itself");
            Assert.That(reader.ResetClassTree(), Is.EqualTo(RefundResult.Refunded));
            Assert.That(reader.FillTrees(), Is.EqualTo(9));

            Assert.That(reader.State.ClassTree.Ranks, Is.EquivalentTo(author.State.ClassTree.Ranks), "the freed points go by the code");
            Assert.That(reader.State.ClassTree.Majors, Is.EquivalentTo(author.State.ClassTree.Majors));
        }

        [Test]
        public void ABrokenOrForeignCodeChangesNothing()
        {
            var author = Hero(FirstClass, 40);
            SpendSome(author);
            var code = author.ShareBuild();
            var other = TestWorld.Data.ClassTrees.Keys.First(id => id != FirstClass && HeroClassNames.ByName.ContainsKey(id));
            var reader = Hero(other, 40);
            var novice = TestWorld.Loop(new FakeInputSource(), new GameState(1) { ClassChosen = false });

            Assert.That(reader.ImportBuild(code), Is.EqualTo(BuildImportResult.OtherClass));
            Assert.That(novice.ImportBuild(code), Is.EqualTo(BuildImportResult.NoClass));
            Assert.That(novice.ShareBuild(), Is.Empty);
            Assert.That(reader.ImportBuild(code.Substring(0, code.Length - 1) + (code.EndsWith("0") ? "1" : "0")), Is.EqualTo(BuildImportResult.BadCode), "the check digits");
            Assert.That(reader.ImportBuild(code.Replace(BuildCode.Prefix, "RW9-")), Is.EqualTo(BuildImportResult.BadCode));
            Assert.That(reader.ImportBuild("hello"), Is.EqualTo(BuildImportResult.BadCode));
            Assert.That(reader.ImportBuild(null), Is.EqualTo(BuildImportResult.BadCode));
            Assert.That(reader.ImportBuild(new string('A', BuildCode.MaxLength + 1)), Is.EqualTo(BuildImportResult.BadCode));
            Assert.That(reader.State.ClassTree.PointsSpent, Is.Zero);
            Assert.That(reader.State.ClassTree.Plan, Is.Empty);
        }

        [Test]
        public void TheAnswerNamesTheClassPointsAndSkills()
        {
            Texts.UseLanguage("ru");
            var loop = Hero(FirstClass, 40);
            SpendSome(loop);
            var build = loop.ReadBuild(loop.ShareBuild())!;

            var text = BuildCodeText.Imported(build, loop.ClassTreeDef!, TestWorld.Data.Gems);

            Assert.That(text, Does.Contain("9 очк."));
            foreach (var result in new[] { BuildImportResult.BadCode, BuildImportResult.NoClass, BuildImportResult.OtherClass, BuildImportResult.Done })
                Assert.That(BuildCodeText.Result(result), Is.Not.Empty);
        }
    }
}
