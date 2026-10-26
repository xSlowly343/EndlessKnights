using System.Linq;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class NotificationQueueTests
    {
        [Test]
        public void KeepsAtMostThreeAndDropsTheOldest()
        {
            var queue = new NotificationQueue();

            for (var i = 0; i < 4; i++)
                queue.Push("n" + i, i * 0.1);

            Assert.That(queue.Items.Select(item => item.Text), Is.EqualTo(new[] { "n1", "n2", "n3" }));
        }

        [Test]
        public void NotificationLeavesAfterThreeSeconds()
        {
            var queue = new NotificationQueue();
            queue.Push("a", 0);
            queue.Push("b", 1);

            Assert.That(queue.Expire(2.9), Is.False);
            Assert.That(queue.Expire(3.0), Is.True);
            Assert.That(queue.Items.Select(item => item.Text), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void SameTextMovesToTheEndInsteadOfRepeating()
        {
            var queue = new NotificationQueue();
            queue.Push("a", 0);
            queue.Push("b", 0);

            queue.Push("a", 1);

            Assert.That(queue.Items.Select(item => item.Text), Is.EqualTo(new[] { "b", "a" }));
            Assert.That(queue.Items[1].ShownAt, Is.EqualTo(1));
        }

        [Test]
        public void ClearEmptiesTheQueue()
        {
            var queue = new NotificationQueue();
            queue.Push("a", 0);

            queue.Clear();

            Assert.That(queue.IsEmpty, Is.True);
        }
    }
}
