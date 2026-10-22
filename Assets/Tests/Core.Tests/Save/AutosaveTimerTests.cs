using Game.Core.Save;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class AutosaveTimerTests
    {
        [Test]
        public void SavesEveryThirtySeconds()
        {
            var timer = new AutosaveTimer();
            var saves = 0;
            for (var frame = 0; frame < 60 * 95; frame++)
                if (timer.Advance(1.0 / 60))
                    saves++;

            Assert.That(saves, Is.EqualTo(3));
        }

        [Test]
        public void NoSaveBeforeTheInterval()
        {
            var timer = new AutosaveTimer(30);

            Assert.That(timer.Advance(29.9), Is.False);
            Assert.That(timer.Advance(0.2), Is.True);
            Assert.That(timer.Advance(0.2), Is.False);
        }
    }
}
