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
        public void AlliesStandBehindTheHeroWithTheirReaction()
        {
            Assert.That(_battle.Allies.Shown, Is.Zero);
            var allies = new[]
            {
                new Game.Core.Ui.StripAlly(0, 1, Game.Core.Party.PartyPresence.Fighting, string.Empty),
                new Game.Core.Ui.StripAlly(3, 2, Game.Core.Party.PartyPresence.Offline, "Привет!"),
                default(Game.Core.Ui.StripAlly),
            };

            _battle.Allies.Show(allies, 2);

            Assert.That(_battle.Allies.Shown, Is.EqualTo(2));
            Assert.That(_battle.Allies.ReactionShown(0), Is.Empty);
            Assert.That(_battle.Allies.ReactionShown(1), Is.EqualTo("Привет!"));

            _battle.Allies.Show(allies, 0);

            Assert.That(_battle.Allies.Shown, Is.Zero);
            Assert.That(_battle.Allies.ReactionShown(1), Is.Empty);
        }

        [Test]
        public void EnemyEffectsAndBossSharesAppearOverTheFight()
        {
            var badges = new[]
            {
                new Game.Core.Ui.EnemyBadge(1, Game.Core.Content.Gems.GemColor.Blue, 7, "Уязвимость"),
                new Game.Core.Ui.EnemyBadge(Game.Core.Ui.PartyStripMarks.NoColor, Game.Core.Content.Gems.GemColor.Red, 2, string.Empty),
            };
            _battle.PartyMarks.ShowBadges(badges, 2);
            Assert.That(_battle.PartyMarks.BadgesShown, Is.EqualTo(2));
            _battle.PartyMarks.ShowBadges(badges, 0);
            Assert.That(_battle.PartyMarks.BadgesShown, Is.Zero);

            var shares = new[] { new Game.Core.Ui.BossShare(0, 0.6, "Вы 60%"), new Game.Core.Ui.BossShare(1, 0.4, "Игрок 2 40%") };
            _battle.PartyMarks.ShowBossShares("Босс повержен · вклад отряда", shares, 2);

            Assert.That(_battle.PartyMarks.CardShown, Is.True);
            Assert.That(_battle.PartyMarks.CardTitle, Is.EqualTo("Босс повержен · вклад отряда"));
            _battle.PartyMarks.Animate(5f);
            Assert.That(_battle.PartyMarks.CardShown, Is.False);
        }

        [Test]
        public void StateLineShowsStageHealthAndGold()
        {
            _battle.ShowState(37, 2, 4, 0.5, 0.62, 0, 1, false, 62.7, 100, BigNumber.FromDouble(12400));

            Assert.That(_battle.StageText, Is.EqualTo("Этап 37"));
            Assert.That(_battle.PercentText, Is.EqualTo("62%"));
            Assert.That(_battle.ResourceText, Is.EqualTo("62"));
            Assert.That(_battle.GoldText, Is.EqualTo(NumberFormatter.Format(BigNumber.FromDouble(12400), NumberFormatSettings.Default)));
            Assert.That(_battle.TimerShown, Is.False);
            Assert.That(_battle.WavesShown, Is.True);
            Assert.That(_battle.WaveSegmentsShown, Is.EqualTo(4));
        }

        [Test]
        public void BossFightShowsTheTimerPill()
        {
            _battle.ShowState(40, 4, 4, 1, 1, 17.91, 0.6, true, 0, 100, BigNumber.Zero);

            Assert.That(_battle.StageText, Is.EqualTo("Этап 40 · БОСС"));
            Assert.That(_battle.TimerShown, Is.True);
            Assert.That(_battle.WavesShown, Is.False);
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
