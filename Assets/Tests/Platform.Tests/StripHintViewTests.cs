using Game.Core.Content;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class StripHintViewTests
    {
        private static readonly TutorialStep Step = new TutorialStep("type", "Печатай", TutorialShow.Start, 0, TutorialDone.Keys, 20, 120);

        private StripHintView _view = null!;

        [SetUp]
        public void SetUp()
        {
            Texts.UseLanguage("ru");
            _view = StripHintView.Create();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_view.gameObject);

        [Test]
        public void ConsentHintHasNoCloseButtonAndReportsAClick()
        {
            var clicked = false;
            _view.HintClicked += () => clicked = true;
            _view.Show(StripHint.For(false, Step));
            Canvas.ForceUpdateCanvases();

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.IsCloseShown, Is.False);
            Assert.That(_view.Text, Is.EqualTo(StripHint.ConsentText));
            Assert.That(_view.TryClick(_view.WorldPointOfText), Is.True);
            Assert.That(clicked, Is.True);
        }

        [Test]
        public void TutorialHintClosesWithTheCross()
        {
            var closed = false;
            _view.CloseClicked += () => closed = true;
            _view.Show(StripHint.For(true, Step));
            Canvas.ForceUpdateCanvases();

            Assert.That(_view.Text, Is.EqualTo(StripHint.TextOf(Step)));
            Assert.That(_view.IsCloseShown, Is.True);
            Assert.That(_view.TryClick(_view.WorldPointOfClose), Is.True);
            Assert.That(closed, Is.True);
        }

        [Test]
        public void NoHintHidesThePillAndLetsClicksThrough()
        {
            _view.Show(StripHint.For(true, Step));
            var point = _view.WorldPointOfText;
            _view.Show(StripHint.None);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.TryClick(point), Is.False);
        }
    }
}
