using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Text
{
    public class StringTableTests
    {
        private const string Valid = @"{
  ""version"": 1,
  ""languages"": [""ru"", ""en""],
  ""strings"": { ""a"": { ""ru"": ""Этап {0}"", ""en"": ""Stage {0}"" } }
}";

        [TearDown]
        public void BackToRussian() => Texts.UseLanguage(StringTable.DefaultLanguage);

        [Test]
        public void RealTableHasEveryTextInBothLanguages()
        {
            var table = StringTable.Load(TestData.ReadGameDataFile(StringTable.FileName));

            Assert.That(table.Languages, Is.EqualTo(new[] { "ru", "en" }));
            Assert.That(table.Count, Is.GreaterThan(200));
        }

        [Test]
        public void EveryStatInTheDataHasALabel()
        {
            var missing = new SortedSet<string>();
            foreach (var file in new[] { "tree.json", "ascendancy.json", "gems.json", "artifacts.json", "synergy.json", "rift.json", "weapons.json" })
            {
                foreach (Match match in Regex.Matches(TestData.ReadGameDataFile(file), "\"stat\": *\"(\\w+)\""))
                {
                    if (!Texts.Has("stat." + match.Groups[1].Value))
                        missing.Add(match.Groups[1].Value);
                }
            }

            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void EnglishSwitchesCoreTexts()
        {
            Assert.That(BookTexts.EmptyRow, Is.EqualTo("пусто"));

            Assert.That(Texts.UseLanguage("en"), Is.True);

            Assert.That(BookTexts.EmptyRow, Is.EqualTo("empty"));
            Assert.That(BookTexts.Locked(10), Is.EqualTo("Unlocks after the stage 10 guardian"));
            Assert.That(ModifierText.Describe(new Modifier(CombatStats.Damage, ModifierKind.More, 0.2, StatTags.Keyboard)), Is.EqualTo("+20% more damage from keys"));
            Assert.That(OfflineReport.Duration(3900), Is.EqualTo("1 h 5 min"));
            Assert.That(PanelSections.Title(PanelSection.Stats), Is.EqualTo("Statistics"));
        }

        [Test]
        public void UnknownLanguageIsRefused()
        {
            Assert.That(Texts.UseLanguage("de"), Is.False);
            Assert.That(Texts.Language, Is.EqualTo("ru"));
        }

        [Test]
        public void UnknownKeyShowsTheKey()
        {
            Assert.That(Texts.Get("no.such.key"), Is.EqualTo("no.such.key"));
        }

        [TestCase(@"""en"": ""Stage""", "uses placeholders {} in 'en' but {0} in 'ru'")]
        [TestCase(@"""en"": """"", "strings.a.en: must not be empty")]
        [TestCase(@"""de"": ""Etappe {0}""", "language 'de' is not in the languages list")]
        public void BrokenEntriesAreReported(string english, string message)
        {
            var json = Valid.Replace(@"""en"": ""Stage {0}""", english);

            var error = Assert.Throws<ContentException>(() => StringTable.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void RussianMustBeListed()
        {
            var error = Assert.Throws<ContentException>(() => StringTable.Load(Valid.Replace(@"[""ru"", ""en""]", @"[""en""]")))!;

            Assert.That(error.Message, Does.Contain("must contain 'ru'"));
        }
    }
}
