using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class ClassTreeTests
    {
        private const string Templates = @"{
  ""version"": 3,
  ""attributes"": [""strength"", ""dexterity""],
  ""respec"": {""cost"": {""curve"": ""exp"", ""base"": 50, ""rate"": 1.3}, ""levelRate"": 1.1, ""attributeShare"": 0.5, ""freeFullResetHours"": 24, ""fullResetShare"": 0.5},
  ""size"": {""min"": 4, ""max"": 6},
  ""templates"": {
    ""attribute"": {""type"": ""attribute"", ""name"": {""ru"": ""Атрибут"", ""en"": ""Attribute""}, ""amount"": 5},
    ""small_damage"": {""theme"": ""damage"", ""type"": ""small"", ""name"": {""ru"": ""Урон"", ""en"": ""Damage""}, ""modifiers"": [{""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.04}]},
    ""mastery_keys"": {""theme"": ""keys"", ""type"": ""mastery"", ""name"": {""ru"": ""М"", ""en"": ""M""}, ""options"": [[{""stat"": ""damage"", ""kind"": ""increased"", ""value"": 0.1}], [{""stat"": ""focusGain"", ""kind"": ""increased"", ""value"": 0.1}]]}
  },
  ""classes"": {
    ""viking"": {
      ""start"": {""name"": {""ru"": ""Huscarl"", ""en"": ""Huscarl""}, ""modifiers"": [{""stat"": ""resourceGain"", ""kind"": ""increased"", ""value"": 0.15}]},
      ""templates"": {
        ""viking_notable_horn"": {""theme"": ""party"", ""type"": ""notable"", ""name"": {""ru"": ""Рог"", ""en"": ""Horn""}, ""modifiers"": [{""stat"": ""damageVsBoss"", ""kind"": ""increased"", ""value"": 0.2}]}
      }
    }
  }
}";

        private const string Tree = @"{
  ""version"": 3,
  ""class"": ""viking"",
  ""nodes"": [
    {""id"": ""viking_0"", ""t"": ""start"", ""x"": 0, ""y"": 0},
    {""id"": ""viking_1"", ""t"": ""small_damage"", ""x"": 1, ""y"": 0},
    {""id"": ""viking_2"", ""t"": ""attribute"", ""x"": 2, ""y"": 0},
    {""id"": ""viking_3"", ""t"": ""viking_notable_horn"", ""x"": 3, ""y"": 0},
    {""id"": ""viking_4"", ""t"": ""mastery_keys"", ""x"": 3, ""y"": 1}
  ],
  ""links"": [[""viking_0"", ""viking_1""], [""viking_1"", ""viking_2""], [""viking_2"", ""viking_3""], [""viking_3"", ""viking_4""]]
}";

        private static PassiveTree Load(string tree, string templates = Templates) =>
            ClassTreeLoader.Load(TreeTemplates.Load(templates), "viking", tree);

        private static string Problem(string tree, string templates = Templates) =>
            Assert.Throws<ContentException>(() => Load(tree, templates))!.Message;

        [Test]
        public void AClassTreeIsBuiltFromCommonAndClassTemplates()
        {
            var tree = Load(Tree);

            Assert.That(tree.Nodes.Count, Is.EqualTo(5));
            Assert.That(tree.LinkCount, Is.EqualTo(4));
            var start = tree.StartOf("viking")!;
            Assert.That(start.Id, Is.EqualTo("viking_0"));
            Assert.That(start.Scope, Is.EqualTo("class:viking"));
            Assert.That(start.Modifiers.Single().Value, Is.EqualTo(0.15));
            Assert.That(tree["viking_1"].Scope, Is.EqualTo(TreeNodeDef.CommonScope));
            Assert.That(tree["viking_3"].ScopeClass, Is.EqualTo("viking"));
            Assert.That(tree["viking_3"].NameEn, Is.EqualTo("Horn"));
            Assert.That(tree["viking_2"].AttributeAmount, Is.EqualTo(5));
            Assert.That(tree["viking_4"].Options.Count, Is.EqualTo(2));
            Assert.That(tree["viking_1"].Neighbours.Select(node => node.Id), Is.EquivalentTo(new[] { "viking_0", "viking_2" }));
            Assert.That(tree.Attributes, Is.EqualTo(new[] { "strength", "dexterity" }));
        }

        [Test]
        public void BrokenClassTreesAreRejectedWithTheReason()
        {
            Assert.That(Problem(Tree.Replace("\"t\": \"small_damage\"", "\"t\": \"small_gold\"")), Does.Contain("unknown template 'small_gold'"));
            Assert.That(Problem(Tree.Replace("\"id\": \"viking_1\"", "\"id\": \"ronin_1\"").Replace("\"viking_1\"", "\"ronin_1\"")), Does.Contain("start with 'viking_'"));
            Assert.That(Problem(Tree.Replace("\"t\": \"small_damage\"", "\"t\": \"start\"")), Does.Contain("one start"));
            Assert.That(Problem(Tree.Replace("[\"viking_1\", \"viking_2\"], ", "")), Does.Contain("one piece"));
            Assert.That(Problem(Tree.Replace("\"t\": \"viking_notable_horn\"", "\"t\": \"small_damage\"")), Does.Contain("'viking_notable_horn' is not placed"));
            Assert.That(Problem(Tree, Templates.Replace("\"min\": 4", "\"min\": 5").Replace("\"max\": 6", "\"max\": 5").Replace("\"min\": 5, \"max\": 5", "\"min\": 6, \"max\": 7")), Does.Contain("expected 6–7"));
            Assert.That(Problem(Tree.Replace("\"class\": \"viking\"", "\"class\": \"ronin\"")), Does.Contain("holds the tree of 'ronin'"));
            Assert.That(Problem(Tree.Replace("\"version\": 3", "\"version\": 2")), Does.Contain("version 2"));
        }

        [Test]
        public void BrokenTemplatesAreRejectedWithTheReason()
        {
            Assert.That(Assert.Throws<ContentException>(() => TreeTemplates.Load(Templates.Replace("viking_notable_horn", "small_damage")))!.Message,
                Does.Contain("already a common template"));
            Assert.That(Assert.Throws<ContentException>(() => TreeTemplates.Load(Templates.Replace("\"roles\"", "\"x\"").Replace("\"theme\": \"damage\",", "\"theme\": \"damage\", \"roles\": [\"tank\"],")))!.Message,
                Does.Contain("unknown role 'tank'"));
            Assert.That(Assert.Throws<ContentException>(() => TreeTemplates.Load(Templates.Replace("\"type\": \"small\"", "\"type\": \"start\"")))!.Message,
                Does.Contain("unknown template type 'start'"));
        }

        [Test]
        public void TheRealTemplatesHaveAStartAndOwnNodesForEveryClass()
        {
            var templates = TreeTemplates.Load(TestData.ReadGameDataFile(TreeTemplates.FileName));

            Assert.That(templates.Classes, Is.EquivalentTo(HeroClassNames.ByName.Keys));
            foreach (var heroClass in templates.Classes)
            {
                Assert.That(templates.TryGet(heroClass, TreeTemplates.StartTemplate, out var start), Is.True, heroClass);
                Assert.That(start.Modifiers, Is.Not.Empty, heroClass);
                Assert.That(templates.OwnOf(heroClass).Count(own => own.Type == TreeNodeType.Notable), Is.GreaterThanOrEqualTo(8), heroClass);
                Assert.That(templates.OwnOf(heroClass).Count(own => own.Type == TreeNodeType.Keystone), Is.EqualTo(1), heroClass);
            }
            Assert.That(templates.Common.Count(template => template.Type == TreeNodeType.Notable), Is.GreaterThan(60));
            Assert.That(templates.Common.Count(template => template.Type == TreeNodeType.Keystone), Is.GreaterThanOrEqualTo(8));
            Assert.That(templates.Common.Single(template => template.Type == TreeNodeType.Attribute).AttributeAmount, Is.EqualTo(5));
        }
    }
}
