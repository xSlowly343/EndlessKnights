using System.Linq;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Tree;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class CommonTreeTests
    {
        private static TreeTemplates Templates => TreeTemplates.Load(TestData.ReadGameDataFile(TreeTemplates.FileName));

        [Test]
        public void SixCommonTreesLoadWithTwentyNodesInFourRows()
        {
            var templates = Templates;

            Assert.That(templates.CommonTrees, Is.EqualTo(new[] { "blade", "agility", "fortune", "party", "colossus", "calm" }));
            foreach (var id in templates.CommonTrees)
            {
                var tree = ClassTreeLoader.LoadCommon(templates, id, TestData.ReadGameDataFile(ClassTreeLoader.CommonFileNameOf(id)));
                Assert.That(tree.Common, Is.True, id);
                Assert.That(tree.HasBar, Is.False, id);
                Assert.That(tree.NameRu, Is.Not.Empty, id);
                Assert.That(tree.AboutRu, Is.Not.Empty, id);
                Assert.That(Enumerable.Range(1, 4).Select(row => tree.Nodes.Count(node => node.Row == row)), Is.EqualTo(new[] { 6, 5, 5, 4 }), id);
                Assert.That(tree.Majors.Select(slot => slot.At), Is.EqualTo(new[] { 20, 45 }), id);
                Assert.That(tree.Majors.All(slot => slot.Options.Count == 3), Is.True, id);
                Assert.That(tree.Nodes.Any(node => node.Passive), Is.False, id);
            }
        }

        [Test]
        public void SeventyPointsReachTheTopRowOfEveryCommonTree()
        {
            var templates = Templates;
            foreach (var id in templates.CommonTrees)
            {
                var tree = ClassTreeLoader.LoadCommon(templates, id, TestData.ReadGameDataFile(ClassTreeLoader.CommonFileNameOf(id)));
                var allocation = new ClassTreeAllocation(id) { PointsEarned = 70 };
                while (allocation.PointsLeft > 0)
                {
                    var next = tree.Nodes.OrderByDescending(node => node.Row).FirstOrDefault(node => allocation.CheckRaise(tree, node.Id) == RankResult.Done);
                    Assert.That(next, Is.Not.Null, $"{id}: nothing to raise after {allocation.PointsSpent} points");
                    allocation.Raise(tree, next!.Id);
                }
                Assert.That(tree.Nodes.Where(node => node.Row == 4).Any(node => allocation.RankOf(node.Id) > 0), Is.True, id);
                Assert.That(allocation.BarSteps(tree), Is.Zero, id);
                Assert.That(allocation.ChooseMajor(tree, 45, tree.Majors[1].Options[0].Id), Is.EqualTo(RankResult.Done), id);
            }
        }

        [Test]
        public void SlotsOpenAtTheirLevelAndGiveAPointPerLevelUpToSeventy()
        {
            var slots = Templates.CommonSlots;

            Assert.That(slots.Select(slot => slot.Level), Is.EqualTo(new[] { 20, 30 }));
            Assert.That(slots[0].OpenAt(19), Is.False);
            Assert.That(slots[0].OpenAt(20), Is.True);
            Assert.That(slots[0].PointsAt(20), Is.Zero);
            Assert.That(slots[0].PointsAt(21), Is.EqualTo(1));
            Assert.That(slots[0].PointsAt(90), Is.EqualTo(70));
            Assert.That(slots[0].PointsAt(100), Is.EqualTo(70));
            Assert.That(slots[1].PointsAt(100), Is.EqualTo(70));
            Assert.That(slots[1].PointsAt(25), Is.Zero);
        }

        [Test]
        public void MajorTalentsKeepAwayFromClassFeaturesMarksAndPaths()
        {
            var data = GameData.Load(TestData.ReadGameDataFile);
            var avoid = data.TreeTemplates.MajorTalentsAvoid;

            Assert.That(avoid, Is.SupersetOf(new[] { "keyHitLightning", "keyHitPoison", "keyHitBurn", "skeletonOnKill", "idleHitsPerSecond", "resourcePerIdleSecond", "overloadDisabled" }));
            foreach (var tree in data.ClassTrees.Values.Concat(data.CommonTrees.Values))
            {
                foreach (var option in tree.Majors.SelectMany(slot => slot.Options))
                    Assert.That(option.Modifiers.Select(modifier => modifier.Stat.Name), Has.None.AnyOf(avoid.ToArray()), option.Id);
            }
        }

        [Test]
        public void EveryTreeStatHasAMechanic()
        {
            var data = GameData.Load(TestData.ReadGameDataFile);
            var known = typeof(Game.Core.Sim.CombatStats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(Game.Core.Stats.StatId))
                .Select(field => ((Game.Core.Stats.StatId)field.GetValue(null)!).Name)
                .Concat(new[] { "strength", "dexterity", "intelligence" })
                .ToHashSet();
            foreach (var tree in data.ClassTrees.Values.Concat(data.CommonTrees.Values))
            {
                var lines = tree.Nodes.SelectMany(node => new[] { node.Template.First, node.Template.Second, node.Template.Third }).SelectMany(line => line.Modifiers)
                    .Concat(tree.Majors.SelectMany(slot => slot.Options).SelectMany(option => option.Modifiers));
                Assert.That(lines.Select(modifier => modifier.Stat.BaseName).Where(stat => !known.Contains(stat)).Distinct(), Is.Empty, tree.Id);
            }
        }

        [Test]
        public void GameDataLoadsTheCommonTrees()
        {
            var data = GameData.Load(TestData.ReadGameDataFile);

            Assert.That(data.CommonTrees.Count, Is.EqualTo(6));
            Assert.That(data.CommonTreeOf("colossus")!.NameEn, Is.EqualTo("Colossus"));
            Assert.That(data.CommonTreeOf("viking"), Is.Null);
        }

        [Test]
        public void ACommonTreeRejectsClassPassivesAndAWrongId()
        {
            var templates = Templates;
            var json = TestData.ReadGameDataFile(ClassTreeLoader.CommonFileNameOf("calm"));

            Assert.That(Assert.Throws<ContentException>(() => ClassTreeLoader.LoadCommon(templates, "blade", json))!.Message, Does.Contain("holds the tree of 'calm'"));
            Assert.That(Assert.Throws<ContentException>(() => ClassTreeLoader.LoadCommon(templates, "calm", json.Replace("\"row\": 1, \"column\": 0,", "\"row\": 1, \"column\": 0, \"passive\": true,")))!.Message, Does.Contain("no class passives"));
        }
    }
}
