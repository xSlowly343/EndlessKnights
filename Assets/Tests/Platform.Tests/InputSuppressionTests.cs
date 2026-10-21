using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class InputSuppressionTests
    {
        [TestCase(@"C:\Program Files\KeePass Password Safe 2\KeePass.exe", true)]
        [TestCase(@"C:\Program Files\Bitwarden\bitwarden.EXE", true)]
        [TestCase(@"C:\Windows\System32\notepad.exe", false)]
        [TestCase(@"D:\Games\LOST ARK\Binaries\Win64\LOSTARK.exe", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void DefaultBlacklist(string? path, bool blocked)
        {
            Assert.That(InputBlacklist.Default().IsBlocked(path), Is.EqualTo(blocked));
        }

        [Test]
        public void BlacklistedForegroundSuppresses()
        {
            var suppression = new InputSuppression(new InputBlacklist(new[] { "secret.exe" }));

            Assert.That(suppression.IsSuppressed(0, () => @"C:\secret.exe"), Is.True);
        }

        [Test]
        public void ForegroundIsCheckedAtMostEveryInterval()
        {
            var suppression = new InputSuppression(new InputBlacklist(new[] { "secret.exe" }), 500);
            var calls = 0;
            string? Foreground()
            {
                calls++;
                return "notepad.exe";
            }

            for (var t = 0; t < 1000; t += 10)
                suppression.IsSuppressed(t, Foreground);

            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void SwitchingAwayLiftsSuppressionAfterNextCheck()
        {
            var suppression = new InputSuppression(new InputBlacklist(new[] { "secret.exe" }), 500);

            Assert.That(suppression.IsSuppressed(0, () => "secret.exe"), Is.True);
            Assert.That(suppression.IsSuppressed(100, () => "notepad.exe"), Is.True);
            Assert.That(suppression.IsSuppressed(600, () => "notepad.exe"), Is.False);
        }

        [Test]
        public void PauseSuppressesEverything()
        {
            var suppression = new InputSuppression(InputBlacklist.Default()) { Paused = true };

            Assert.That(suppression.IsSuppressed(0, () => "notepad.exe"), Is.True);
        }

        [Test]
        public void DrainReportsSuppression()
        {
            Assert.That(new InputCounters().Drain(0, suppressed: true).Suppressed, Is.True);
        }
    }
}
