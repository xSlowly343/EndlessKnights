using System.Linq;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class ClassTreeTests
    {
        private const string Templates = @"{
  ""version"": 4,
  ""respec"": {""cost"": {""curve"": ""exp"", ""base"": 50, ""rate"": 1.3}, ""levelRate"": 1.1, ""attributeShare"": 0.5, ""freeFullResetHours"": 24, ""fullResetShare"": 0.5},
  ""rows"": [0, 3],
  ""nodes"": 3,
  ""classes"": [""viking""],
  ""bar"": {""every"": 2, ""majorsAt"": [4], ""majorOptions"": 2},
  ""templates"": {
    ""crit"": {""name"": {""ru"": ""Крит"", ""en"": ""Crit""}, ""a"": {""modifiers"": [{""stat"": ""critChance"", ""kind"": ""increased"", ""value"": 0.03}]}, ""b"": {""modifiers"": [{""stat"": ""critMultiplier"", ""kind"": ""flat"", ""value"": 0.05}]}, ""c"": {""text"": {""ru"": ""т"", ""en"": ""t""}, ""waits"": ""critFocusPause""}}
  }
}";

        private const string Tree = @"{
  ""version"": 4,
  ""class"": ""viking"",
  ""barPassives"": [[{""stat"": ""strength"", ""kind"": ""flat"", ""value"": 3}], [{""stat"": ""resourceGain"", ""kind"": ""increased"", ""value"": 0.02}]],
  ""nodes"": [
    {""id"": ""viking_axe"", ""row"": 1, ""column"": 0, ""name"": {""ru"": ""Топор"", ""en"": ""Axe""}, ""a"": {""modifiers"": [{""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.04}]}, ""b"": {""modifiers"": [{""stat"": ""critChance"", ""kind"": ""increased"", ""value"": 0.02}]}, ""c"": {""modifiers"": [{""stat"": ""keyStrikeEvery"", ""kind"": ""flat"", ""value"": 30}]}},
    {""id"": ""viking_crit"", ""row"": 1, ""column"": 1, ""t"": ""crit""},
    {""id"": ""viking_swing"", ""row"": 2, ""column"": 0, ""parent"": ""viking_axe"", ""name"": {""ru"": ""Замах"", ""en"": ""Swing""}, ""a"": {""modifiers"": [{""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.06}]}, ""b"": {""modifiers"": [{""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.01}]}, ""c"": {""modifiers"": [{""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.02}]}}
  ],
  ""majors"": [
    {""at"": 4, ""options"": [
      {""id"": ""viking_cool"", ""name"": {""ru"": ""Х"", ""en"": ""C""}, ""modifiers"": [{""stat"": ""critChance"", ""kind"": ""increased"", ""value"": 0.1}]},
      {""id"": ""viking_warm"", ""name"": {""ru"": ""Р"", ""en"": ""W""}, ""text"": {""ru"": ""р"", ""en"": ""w""}, ""waits"": ""warmUp""}
    ]}
  ]
}";

        private static ClassTree Load(string tree = Tree, string templates = Templates) =>
            ClassTreeLoader.Load(TreeTemplates.Load(templates), "viking", tree);

        private static string Problem(string tree, string templates = Templates) =>
            Assert.Throws<ContentException>(() => Load(tree, templates))!.Message;

        private static double Sum(System.Collections.Generic.IEnumerable<Modifier> modifiers, string stat) =>
            modifiers.Where(modifier => modifier.Stat == new StatId(stat)).Sum(modifier => modifier.Value);

        [Test]
        public void TheFirstLineRepeatsOnRanksTwoAndFourAndNewLinesOpenOnThreeAndFive()
        {
            var axe = Load()["viking_axe"];

            Assert.That(Sum(axe.ModifiersAt(1), "damage"), Is.EqualTo(0.04).Within(1e-9));
            Assert.That(Sum(axe.ModifiersAt(2), "damage"), Is.EqualTo(0.08).Within(1e-9));
            Assert.That(Sum(axe.ModifiersAt(2), "critChance"), Is.Zero);
            Assert.That(Sum(axe.ModifiersAt(3), "critChance"), Is.EqualTo(0.02).Within(1e-9));
            Assert.That(Sum(axe.ModifiersAt(4), "damage"), Is.EqualTo(0.12).Within(1e-9));
            Assert.That(Sum(axe.ModifiersAt(4), "keyStrikeEvery"), Is.Zero);
            Assert.That(Sum(axe.ModifiersAt(5), "keyStrikeEvery"), Is.EqualTo(30));
        }

        [Test]
        public void RowsOpenByPointsBelowAndNodesNeedTheirParent()
        {
            var tree = Load();
            var allocation = new ClassTreeAllocation("viking") { PointsEarned = 20 };

            Assert.That(allocation.Raise(tree, "viking_swing"), Is.EqualTo(RankResult.RowClosed));
            Assert.That(allocation.Raise(tree, "viking_crit"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Raise(tree, "viking_crit"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Raise(tree, "viking_crit"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Raise(tree, "viking_swing"), Is.EqualTo(RankResult.NeedsParent), "the row is open, the parent is not taken");
            Assert.That(allocation.Raise(tree, "viking_axe"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Raise(tree, "viking_swing"), Is.EqualTo(RankResult.Done));

            Assert.That(allocation.Lower(tree, "viking_axe"), Is.EqualTo(RankResult.HoldsChild));
            Assert.That(allocation.Lower(tree, "viking_crit"), Is.EqualTo(RankResult.Done), "4 points stay below row 2");
            Assert.That(allocation.Lower(tree, "viking_crit"), Is.EqualTo(RankResult.HoldsRow));
            Assert.That(allocation.Lower(tree, "viking_swing"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Lower(tree, "viking_axe"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.PointsSpent, Is.EqualTo(2));
        }

        [Test]
        public void ANodeStopsAtFiveRanksAndPointsRunOut()
        {
            var tree = Load();
            var allocation = new ClassTreeAllocation("viking") { PointsEarned = 6 };

            for (var rank = 0; rank < 5; rank++)
                Assert.That(allocation.Raise(tree, "viking_axe"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Raise(tree, "viking_axe"), Is.EqualTo(RankResult.MaxRank));
            Assert.That(allocation.Raise(tree, "viking_crit"), Is.EqualTo(RankResult.Done));
            Assert.That(allocation.Raise(tree, "viking_crit"), Is.EqualTo(RankResult.NoPoints));
        }

        [Test]
        public void TheBarGivesPassivesAndMajorTalentsAtItsMarks()
        {
            var tree = Load();
            var allocation = new ClassTreeAllocation("viking") { PointsEarned = 10 };
            allocation.Raise(tree, "viking_axe");
            allocation.Raise(tree, "viking_axe");
            allocation.Raise(tree, "viking_axe");

            Assert.That(allocation.ChooseMajor(tree, 4, "viking_cool"), Is.EqualTo(RankResult.MarkNotReached));
            Assert.That(Sum(allocation.Modifiers(tree), "strength"), Is.EqualTo(3), "one bar step at 2 points");
            allocation.Raise(tree, "viking_axe");
            Assert.That(Sum(allocation.Modifiers(tree), "resourceGain"), Is.EqualTo(0.02).Within(1e-9), "the second step gives the second passive");
            Assert.That(allocation.ChooseMajor(tree, 4, "viking_nope"), Is.EqualTo(RankResult.UnknownMajor));
            Assert.That(allocation.ChooseMajor(tree, 4, "viking_cool"), Is.EqualTo(RankResult.Done));
            Assert.That(Sum(allocation.Modifiers(tree), "critChance"), Is.EqualTo(0.12).Within(1e-9), "rank 3 of the axe and the talent");
            Assert.That(allocation.ChooseMajor(tree, 4, "viking_warm"), Is.EqualTo(RankResult.Done), "a major talent changes for free");
            Assert.That(Sum(allocation.Modifiers(tree), "critChance"), Is.EqualTo(0.02).Within(1e-9));
            allocation.ChooseMajor(tree, 4, "viking_cool");
            allocation.Lower(tree, "viking_axe");
            Assert.That(allocation.MajorActive(4), Is.False, "below the mark the talent sleeps");
            Assert.That(Sum(allocation.Modifiers(tree), "critChance"), Is.EqualTo(0.02).Within(1e-9));
        }

        [Test]
        public void BrokenTreesAreRejectedWithTheReason()
        {
            Assert.That(Problem(Tree.Replace("\"t\": \"crit\"", "\"t\": \"gold\"")), Does.Contain("unknown common template 'gold'"));
            Assert.That(Problem(Tree.Replace("\"viking_crit\"", "\"ronin_crit\"")), Does.Contain("start with 'viking_'"));
            Assert.That(Problem(Tree.Replace("\"row\": 1, \"column\": 1", "\"row\": 1, \"column\": 0")), Does.Contain("already has a node in column 0"));
            Assert.That(Problem(Tree.Replace("\"row\": 2, \"column\": 0, \"parent\"", "\"row\": 1, \"column\": 2, \"parent\"")), Does.Contain("lower row"));
            Assert.That(Problem(Tree.Replace("\"parent\": \"viking_axe\"", "\"parent\": \"viking_swing\"")), Does.Contain("listed earlier"));
            Assert.That(Problem(Tree, Templates.Replace("\"nodes\": 3", "\"nodes\": 4")), Does.Contain("expected 4"));
            Assert.That(Problem(Tree, Templates.Replace("\"rows\": [0, 3]", "\"rows\": [0, 3, 6]")), Does.Contain("row 3 has no nodes"));
            Assert.That(Problem(Tree, Templates.Replace("\"majorsAt\": [4]", "\"majorsAt\": [6]")), Does.Contain("is at 6 points"));
            Assert.That(Problem(Tree, Templates.Replace("\"majorOptions\": 2", "\"majorOptions\": 3")), Does.Contain("offers 3"));
            Assert.That(Problem(Tree.Replace("\"waits\": \"warmUp\"", "\"x\": 1")), Does.Contain("needs modifiers, or a text and the mechanic"));
            Assert.That(Problem(Tree.Replace("\"version\": 4", "\"version\": 3")), Does.Contain("version 3"));
            Assert.That(Assert.Throws<ContentException>(() => TreeTemplates.Load(Templates.Replace("\"rows\": [0, 3]", "\"rows\": [2, 3]")))!.Message, Does.Contain("threshold is 0"));
        }

        [Test]
        public void TheHuscarlTreeFromTheDesignLoadsAndAllPointsCanBeSpent()
        {
            var templates = TreeTemplates.Load(TestData.ReadGameDataFile(TreeTemplates.FileName));
            var tree = ClassTreeLoader.Load(templates, "viking", TestData.ReadGameDataFile(ClassTreeLoader.FileNameOf("viking")));

            Assert.That(tree.Nodes.Count, Is.EqualTo(50));
            Assert.That(Enumerable.Range(1, 7).Select(row => tree.Nodes.Count(node => node.Row == row)), Is.EqualTo(new[] { 10, 9, 8, 7, 6, 5, 5 }));
            Assert.That(tree.RowThresholds, Is.EqualTo(new[] { 0, 5, 15, 30, 50, 75, 105 }));
            Assert.That(tree.Majors.Select(slot => slot.At), Is.EqualTo(new[] { 20, 50, 90, 140 }));
            Assert.That(tree.Majors.All(slot => slot.Options.Count == 3), Is.True);
            Assert.That(tree.MaxRanks, Is.EqualTo(250));
            Assert.That(tree.Nodes.Count(node => node.Common), Is.InRange(15, 25));

            var allocation = new ClassTreeAllocation("viking") { PointsEarned = 180 };
            while (allocation.PointsLeft > 0)
            {
                var next = tree.Nodes.OrderByDescending(node => node.Row).FirstOrDefault(node => allocation.CheckRaise(tree, node.Id) == RankResult.Done);
                Assert.That(next, Is.Not.Null, $"nothing to raise after {allocation.PointsSpent} points");
                allocation.Raise(tree, next!.Id);
            }
            Assert.That(tree.Nodes.Where(node => node.Row == 7).Any(node => allocation.RankOf(node.Id) > 0), Is.True, "the top row is reached");
            Assert.That(allocation.BarSteps(tree), Is.EqualTo(18));
        }
    }
}
