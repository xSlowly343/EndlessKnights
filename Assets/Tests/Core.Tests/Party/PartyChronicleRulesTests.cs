using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyChronicleRulesTests
    {
        private const string Valid = @"{
  ""version"": 1,
  ""flushSeconds"": 60,
  ""maxRecords"": 50,
  ""night"": { ""utcOffsetMinutes"": 180, ""fromHour"": 0, ""toHour"": 6 },
  ""feats"": [
    { ""id"": ""first_steps"", ""kind"": ""bossesTogether"", ""levels"": [10], ""banner"": [1] },
    { ""id"": ""rift_conquerors"", ""kind"": ""riftDepth"", ""levels"": [100, 200], ""title"": ""owls"", ""tracked"": false }
  ]
}";

        [Test]
        public void TheGameFileLoadsAndEveryFeatHasItsTexts()
        {
            _ = TestWorld.Real;
            var rules = PartyChronicleRules.Load(TestData.ReadGameDataFile(PartyChronicleRules.FileName));

            Assert.That(rules.Feats.Select(feat => feat.Id), Does.Contain("first_steps").And.Contain("old_guard").And.Contain("full_orchestra"));
            foreach (var language in new[] { "en", "ru" })
            {
                Texts.UseLanguage(language);
                foreach (var feat in rules.Feats)
                {
                    Assert.That(Texts.Get("chronicle.feat." + feat.Id), Does.Not.StartWith("chronicle."), feat.Id + " " + language);
                    Assert.That(Texts.Get("chronicle.goal." + feat.Kind), Does.Not.StartWith("chronicle."), feat.Kind + " " + language);
                    if (feat.Title != null)
                        Assert.That(Texts.Get("chronicle.title." + feat.Title), Does.Not.StartWith("chronicle."), feat.Title + " " + language);
                }
            }
        }

        [Test]
        public void FeatsAreRead()
        {
            var rules = PartyChronicleRules.Load(Valid);

            Assert.That(rules.FlushSeconds, Is.EqualTo(60));
            Assert.That(rules.MaxRecords, Is.EqualTo(50));
            Assert.That(rules.Feats[0].Banner, Is.EqualTo(new[] { 1 }));
            Assert.That(rules.Feats[0].Tracked, Is.True);
            Assert.That(rules.Feats[1].Title, Is.EqualTo("owls"));
            Assert.That(rules.NightUtcOffsetMinutes, Is.EqualTo(180), "the night is counted by Moscow time");
            Assert.That(rules.NightFromHour, Is.EqualTo(0));
            Assert.That(rules.NightToHour, Is.EqualTo(6));
            Assert.That(rules.Feats[1].Tracked, Is.False);
        }

        [TestCase(@"""levels"": [10], ""banner"": [1]", @"""levels"": [10, 5], ""banner"": [1, 2]", "levels")]
        [TestCase(@"""levels"": [10], ""banner"": [1]", @"""levels"": [10], ""banner"": [1, 2]", "banner")]
        [TestCase(@"""levels"": [100, 200], ""title"": ""owls"", ""tracked"": false", @"""levels"": [100, 200], ""title"": ""owls""", "not counted yet")]
        [TestCase(@"""toHour"": 6", @"""toHour"": 0", "toHour")]
        [TestCase(@"""kind"": ""bossesTogether""", @"""kind"": ""dragons""", "unknown kind")]
        [TestCase(@"""id"": ""rift_conquerors""", @"""id"": ""first_steps""", "new, non-empty id")]
        public void BadFeatsAreRejected(string from, string to, string message)
        {
            var error = Assert.Throws<ContentException>(() => PartyChronicleRules.Load(Valid.Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }

        [Test]
        public void ClassRolesInPartyJsonFollowTheHeroClassOrder()
        {
            var text = TestData.ReadGameDataFile("party.json");
            var roles = Regex.Match(text, "\"classRoles\"\\s*:\\s*\\{([^}]*)\\}").Groups[1].Value;
            var names = Regex.Matches(roles, "\"([a-z_]+)\"\\s*:").Cast<Match>().Select(match => match.Groups[1].Value).ToArray();

            Assert.That(names, Is.EqualTo(Enum.GetValues(typeof(HeroClass)).Cast<HeroClass>().Select(HeroClassNames.Of).ToArray()),
                "the server reads roles by class number in this order");
        }
    }
}
