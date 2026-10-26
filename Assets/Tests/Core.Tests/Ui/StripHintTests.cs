using Game.Core.Content;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class StripHintTests
    {
        private static readonly TutorialStep Step = new TutorialStep("type", "Печатай", TutorialShow.Start, 0, TutorialDone.Keys, 20, 120);

        [Test]
        public void MissingConsentWinsOverTheTutorial()
        {
            Texts.UseLanguage("ru");
            var hint = StripHint.For(false, Step);

            Assert.That(hint.Kind, Is.EqualTo(StripHintKind.Consent));
            Assert.That(hint.Text, Is.EqualTo(StripHint.ConsentText));
            Assert.That(hint.CanClose, Is.False);
        }

        [Test]
        public void TutorialStepShowsInTheChosenLanguage()
        {
            Texts.UseLanguage("en");
            var hint = StripHint.For(true, Step);

            Assert.That(hint.Kind, Is.EqualTo(StripHintKind.Tutorial));
            Assert.That(hint.Text, Is.EqualTo("Type anywhere: every key is a hit"));
            Assert.That(hint.CanClose, Is.True);
            Assert.That(StripHint.For(true, null).IsShown, Is.False);
        }

        [Test]
        public void StepWithoutAStringFallsBackToItsOwnText()
        {
            var step = new TutorialStep("unknown", "Своя подсказка", TutorialShow.Start, 0, TutorialDone.Keys, 1, 10);

            Assert.That(StripHint.TextOf(step), Is.EqualTo("Своя подсказка"));
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void EveryRealStepIsTranslatedAndFitsTheStrip(string language)
        {
            Texts.UseLanguage(language);
            var rules = TutorialRules.Load(TestData.ReadGameDataFile(TutorialRules.FileName));

            foreach (var step in rules.Steps)
            {
                Assert.That(Texts.Has("tutorial." + step.Id), Is.True, step.Id);
                Assert.That(StripHint.TextOf(step).Length, Is.LessThanOrEqualTo(TutorialRules.MaxTextLength), step.Id);
            }
            Assert.That(StripHint.ConsentText.Length, Is.LessThanOrEqualTo(TutorialRules.MaxTextLength));
        }

        [TearDown]
        public void RestoreLanguage() => Texts.UseLanguage("ru");
    }
}
