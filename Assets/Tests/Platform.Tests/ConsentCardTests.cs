using Game.Core.Text;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class ConsentCardTests
    {
        private PanelCanvas _panel = null!;

        [SetUp]
        public void SetUp()
        {
            Texts.UseLanguage("ru");
            _panel = PanelCanvas.Create();
            _panel.ShowConsent();
            _panel.Place(0f, true);
            _panel.FinishTween();
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            Texts.UseLanguage("ru");
            Object.DestroyImmediate(_panel.gameObject);
        }

        [Test]
        public void ConsentShowsWhatIsCountedAndWhatIsNot()
        {
            Assert.That(_panel.IsShowingConsent, Is.True);
            Assert.That(_panel.IsShowingReport, Is.False);
            Assert.That(_panel.Consent.Title, Is.EqualTo(ConsentTexts.Title));
            for (var i = 0; i < ConsentCard.CountedLines; i++)
                Assert.That(_panel.Consent.CountedText(i), Is.EqualTo(ConsentTexts.Counted[i]));
            for (var i = 0; i < ConsentCard.NotCountedLines; i++)
                Assert.That(_panel.Consent.NotCountedText(i), Is.EqualTo(ConsentTexts.NotCounted[i]));
            Assert.That(_panel.HeightPx, Is.EqualTo(PanelCanvas.ConsentFrame.HeightPx));
        }

        [Test]
        public void AcceptAndDeclineAreReported()
        {
            var accepted = false;
            var declined = false;
            _panel.ConsentAccepted += () => accepted = true;
            _panel.ConsentDeclined += () => declined = true;

            Assert.That(_panel.TryClick(_panel.Consent.WorldPointOfDecline), Is.True);
            Assert.That(declined, Is.True);
            Assert.That(accepted, Is.False);

            Assert.That(_panel.TryClick(_panel.Consent.WorldPointOfAccept), Is.True);
            Assert.That(accepted, Is.True);
        }

        [Test]
        public void ConsentFollowsTheLanguage()
        {
            Texts.UseLanguage("en");
            _panel.ShowConsent();

            Assert.That(_panel.Consent.Title, Is.EqualTo("What the game counts and what it doesn't"));
        }

        [Test]
        public void OpeningASectionHidesTheConsent()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Hero);
            _panel.Show(navigator);

            Assert.That(_panel.IsShowingConsent, Is.False);
            Assert.That(_panel.Consent.IsShown, Is.False);
        }
    }
}
