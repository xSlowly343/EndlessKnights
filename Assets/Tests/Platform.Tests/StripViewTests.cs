using System.IO;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;
using Game.Presentation.Battle;
using Game.Presentation.Strip;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class StripViewTests
    {
        private BattleView _battle = null!;
        private SkillBarView _skillBar = null!;
        private GameLoop _loop = null!;

        [SetUp]
        public void SetUp()
        {
            var data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            Texts.UseLanguage("ru");
            _loop = new GameLoop(data, new FakeInputSource(), new GameState { Class = HeroClass.Viking }, new StatSheet());
            _loop.Book.GrantStartingGems();
            _battle = BattleView.Create();
            _skillBar = SkillBarView.Create(_battle.transform);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_battle.gameObject);

        [Test]
        public void StateLineShowsStageHealthAndGold()
        {
            _battle.ShowState(37, 4, 10, 0.62, 0, 1, false, 0.5, 2.14, 0.5, BigNumber.FromDouble(12400));

            Assert.That(_battle.StageText, Is.EqualTo("Этап 37"));
            Assert.That(_battle.PercentText, Is.EqualTo("62%"));
            Assert.That(_battle.MultiplierText, Is.EqualTo("×2.1"));
            Assert.That(_battle.GoldText, Is.EqualTo(NumberFormatter.Format(BigNumber.FromDouble(12400), NumberFormatSettings.Default)));
            Assert.That(_battle.TimerShown, Is.False);
        }

        [Test]
        public void BossFightShowsTheTimerPill()
        {
            _battle.ShowState(40, 0, 10, 1, 17.91, 0.6, true, 0, 1, 0, BigNumber.Zero);

            Assert.That(_battle.StageText, Is.EqualTo("Этап 40 · БОСС"));
            Assert.That(_battle.TimerShown, Is.True);
            Assert.That(_battle.TimerText, Is.EqualTo("18.0 с"));
        }

        [Test]
        public void FocusButtonSwapsItsGlyphAndTakesClicks()
        {
            var button = FocusModeButton.Create(StripSpace.AtPx(419f, 20f));
            Physics2D.SyncTransforms();

            Assert.That(button.Glyph, Is.SameAs(StripIcons.Focus));
            Assert.That(button.Contains(StripSpace.AtPx(419f, 20f)), Is.True);
            Assert.That(button.Contains(StripSpace.AtPx(380f, 20f)), Is.False);
            button.ShowPassingClicks(true);
            Assert.That(button.Glyph, Is.SameAs(StripIcons.NoFocus));

            Object.DestroyImmediate(button.gameObject);
        }

        [Test]
        public void InputDebugOverlayPrintsOnlyCounters()
        {
            var overlay = InputDebugOverlay.Create(StripSpace.BottomLeft(0.3f));

            Assert.That(overlay.Text, Does.StartWith("keys 0"));

            Object.DestroyImmediate(overlay.gameObject);
        }

        [Test]
        public void ClickingASkillIconRequestsThatSlot()
        {
            var clicked = -1;
            _skillBar.SlotClicked += slot => clicked = slot;
            _skillBar.Show(_loop.State.Skills, _loop.State.Resource);
            Canvas.ForceUpdateCanvases();

            Assert.That(_skillBar.IsShown(0), Is.True);
            Assert.That(_skillBar.LabelOf(0), Is.Not.Empty);
            Assert.That(_skillBar.TryClick(_skillBar.WorldPointOf(0)), Is.True);
            Assert.That(clicked, Is.EqualTo(0));
            Assert.That(_skillBar.TryClick(_skillBar.WorldPointOf(0) + new Vector2(0f, -5f)), Is.False);
        }
    }
}
