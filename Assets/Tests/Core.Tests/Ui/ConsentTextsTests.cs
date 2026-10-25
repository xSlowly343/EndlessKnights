using System.Linq;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class ConsentTextsTests
    {
        [TestCase("ru")]
        [TestCase("en")]
        public void EveryLineIsTranslated(string language)
        {
            Texts.UseLanguage(language);

            var all = ConsentTexts.Counted.Concat(ConsentTexts.NotCounted)
                .Concat(new[] { ConsentTexts.Title, ConsentTexts.Accept, ConsentTexts.Decline, ConsentTexts.Declined, ConsentTexts.NetworkNote, ConsentTexts.CountedHeading, ConsentTexts.NotCountedHeading })
                .ToList();

            Assert.That(all, Has.All.Not.Empty);
            Assert.That(all, Has.None.StartsWith("consent."));
        }

        [Test]
        public void HeadingsFollowTheLanguage()
        {
            Texts.UseLanguage("en");
            Assert.That(ConsentTexts.CountedHeading, Is.EqualTo("Counted"));
            Texts.UseLanguage("ru");
            Assert.That(ConsentTexts.CountedHeading, Is.EqualTo("Считаем"));
        }

        [TearDown]
        public void RestoreLanguage() => Texts.UseLanguage("ru");
    }
}
