using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class NotificationPillsTests
    {
        private NotificationPills _pills = null!;

        [SetUp]
        public void SetUp() => _pills = NotificationPills.Create();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_pills.gameObject);

        [Test]
        public void ShowsEveryQueuedNotification()
        {
            var queue = new NotificationQueue();
            queue.Push("a", 0);
            queue.Push("b", 0.5);

            _pills.Show(queue.Items, 1);

            Assert.That(_pills.gameObject.activeSelf, Is.True);
            Assert.That(_pills.ShownCount, Is.EqualTo(2));
            Assert.That(_pills.TextAt(1), Is.EqualTo("b"));
        }

        [Test]
        public void EmptyQueueHidesThePills()
        {
            _pills.Show(new NotificationQueue().Items, 0);

            Assert.That(_pills.gameObject.activeSelf, Is.False);
        }
    }
}
