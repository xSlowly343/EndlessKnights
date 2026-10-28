using System.Linq;
using Game.Core.Content;
using Game.Core.Settings;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PrivacyTextsTests
    {
        [TestCase("ru")]
        [TestCase("en")]
        public void PolicyAndAgreementFitTheReportCard(string language)
        {
            Texts.UseLanguage(language);

            foreach (var lines in new[] { PrivacyTexts.Policy, PrivacyTexts.Eula })
            {
                Assert.That(lines.Count, Is.LessThanOrEqualTo(ChangeLog.MaxLines));
                Assert.That(lines, Has.All.Not.Empty);
                Assert.That(lines.Select(line => line.Length), Has.All.LessThanOrEqualTo(ChangeLog.MaxLineLength));
                Assert.That(lines, Has.None.StartsWith("privacy.").And.None.StartsWith("eula."));
            }
        }

        [Test]
        public void TitlesCarryThePolicyVersion()
        {
            Texts.UseLanguage("ru");

            Assert.That(PrivacyTexts.PolicyTitle, Is.EqualTo("Политика приватности (версия " + PrivacyPolicy.CurrentVersion + ")"));
            Assert.That(PrivacyTexts.Title(SettingId.Eula), Does.StartWith("Пользовательское соглашение"));
            Assert.That(PrivacyTexts.Lines(SettingId.PrivacyPolicy), Is.EqualTo(PrivacyTexts.Policy));
            Assert.That(PrivacyTexts.GotIt, Is.Not.Empty.And.Not.StartWith("report."));
        }

        [Test]
        public void PolicySaysKeyCodesAndTextAreNotKept()
        {
            Texts.UseLanguage("ru");

            Assert.That(string.Join(" ", PrivacyTexts.Policy), Does.Contain("Коды клавиш").And.Contain("текст"));
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void ConsentNoteNamesThePolicyVersion(string language)
        {
            Texts.UseLanguage(language);

            Assert.That(ConsentTexts.PolicyNote, Does.Contain(PrivacyPolicy.CurrentVersion.ToString()));
            Assert.That(ConsentTexts.Note, Does.StartWith(ConsentTexts.NetworkNote).And.EndWith(ConsentTexts.PolicyNote));
        }

        [Test]
        public void PrivacyTabOpensThePolicyAndTheAgreement()
        {
            Texts.UseLanguage("ru");

            Assert.That(SettingsScreen.Rows(2), Does.Contain(SettingId.PrivacyPolicy));
            Assert.That(SettingsScreen.Rows(2), Does.Contain(SettingId.Eula));
            Assert.That(SettingsScreen.KindOf(SettingId.PrivacyPolicy), Is.EqualTo(SettingKind.Action));
            Assert.That(SettingsScreen.KindOf(SettingId.Eula), Is.EqualTo(SettingKind.Action));
            Assert.That(SettingsScreen.Change(SettingId.Eula, new GameSettings(), 1), Is.True);
            Assert.That(PanelSections.SubTabs(PanelSection.Settings)[2], Is.EqualTo("Приватность"));
        }

        [TearDown]
        public void RestoreLanguage() => Texts.UseLanguage("ru");
    }
}
