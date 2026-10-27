using Game.Core.Diagnostics;
using NUnit.Framework;

namespace Game.Core.Tests.Diagnostics
{
    public class LogScrubberTests
    {
        [TestCase(@"Save failed: C:\Users\Ivan Petrov\AppData\Roaming\RiftWarden\save.json", @"Save failed: %USER%\AppData\Roaming\RiftWarden\save.json")]
        [TestCase("Data: C:/Users/ivan/AppData/LocalLow/RiftWarden/Player.log", "Data: %USER%/AppData/LocalLow/RiftWarden/Player.log")]
        [TestCase(@"at D:\Documents and Settings\olga\game.cs:12", @"at %USER%\game.cs:12")]
        [TestCase("open /home/sergey/build/log.txt", "open %USER%/build/log.txt")]
        public void UserFoldersBecomeAMarkerEvenWithoutKnowingTheUser(string line, string expected)
        {
            Assert.That(new LogScrubber().Scrub(line), Is.EqualTo(expected));
        }

        [Test]
        public void KnownProfileIsReplacedWhateverTheSlashes()
        {
            var scrubber = new LogScrubber(@"E:\Profiles\ivan.p", "ivan.p");

            Assert.That(scrubber.Scrub("Path E:/Profiles/ivan.p/save.json"), Is.EqualTo("Path %USER%/save.json"));
            Assert.That(scrubber.Scrub(@"Path e:\profiles\IVAN.P\save.json"), Is.EqualTo(@"Path %USER%\save.json"));
        }

        [Test]
        public void UserNameAloneIsReplacedOnlyAsAWholeWord()
        {
            var scrubber = new LogScrubber(null, "Ivan");

            Assert.That(scrubber.Scrub("Steam user Ivan joined"), Is.EqualTo("Steam user %USER% joined"));
            Assert.That(scrubber.Scrub("Ivanhoe stays"), Is.EqualTo("Ivanhoe stays"));
        }

        [Test]
        public void ShortUserNamesAreNotReplacedInsideText()
        {
            Assert.That(new LogScrubber(null, "al").Scrub("all values"), Is.EqualTo("all values"));
        }

        [TestCase("Pressed KeyCode.W in editor", "Pressed [key] in editor")]
        [TestCase("raw vkey=0x41 make", "raw [key] make")]
        [TestCase("scancode: 30 down", "[key] down")]
        public void KeyCodesAreRedacted(string line, string expected)
        {
            Assert.That(new LogScrubber().Scrub(line), Is.EqualTo(expected));
        }

        [Test]
        public void WordsThatOnlyStartLikeKeyNamesStay()
        {
            Assert.That(new LogScrubber().Scrub("vkontakte keycodes"), Is.EqualTo("vkontakte keycodes"));
        }

        [Test]
        public void LongLinesAreCut()
        {
            Assert.That(new LogScrubber().Scrub(new string('x', 2000)).Length, Is.EqualTo(LogScrubber.MaxLineLength));
        }
    }
}
