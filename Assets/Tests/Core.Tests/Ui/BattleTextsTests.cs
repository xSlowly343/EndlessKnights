using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class BattleTextsTests
    {
        [Test]
        public void StageLineMarksTheBoss()
        {
            Texts.UseLanguage("ru");

            Assert.That(BattleTexts.Stage(37, false), Is.EqualTo("Этап 37"));
            Assert.That(BattleTexts.Stage(37, true), Is.EqualTo("Этап 37 · БОСС"));
        }

        [Test]
        public void FocusMultiplierRoundsDownToTenths()
        {
            Assert.That(BattleTexts.MultiplierTenths(2.19), Is.EqualTo(21));
            Assert.That(BattleTexts.MultiplierTenths(1.0), Is.EqualTo(10));
            Assert.That(BattleTexts.Multiplier(21), Is.EqualTo("×2.1"));
            Assert.That(BattleTexts.Multiplier(10), Is.EqualTo("×1.0"));
        }

        [Test]
        public void BossTimerRoundsUpToTenths()
        {
            Texts.UseLanguage("ru");

            Assert.That(BattleTexts.Tenths(17.91), Is.EqualTo(180));
            Assert.That(BattleTexts.Tenths(-1), Is.EqualTo(0));
            Assert.That(BattleTexts.Seconds(180), Is.EqualTo("18.0 с"));
        }

        [Test]
        public void HealthPercentNeverShowsZeroWhileTheEnemyLives()
        {
            Assert.That(BattleTexts.PercentOf(0.001), Is.EqualTo(1));
            Assert.That(BattleTexts.PercentOf(0.62), Is.EqualTo(62));
            Assert.That(BattleTexts.PercentOf(1.5), Is.EqualTo(100));
            Assert.That(BattleTexts.Percent(62), Is.EqualTo("62%"));
        }
    }
}
