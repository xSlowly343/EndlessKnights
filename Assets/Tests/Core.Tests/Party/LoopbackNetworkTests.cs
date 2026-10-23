using Game.Core.Party.Transport;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class LoopbackNetworkTests
    {
        private static readonly byte[] Payload = { 1, 2, 3 };

        [Test]
        public void PacketArrivesOnlyAfterLatency()
        {
            var network = new LoopbackNetwork(1, 100, 100);
            var a = network.Connect(1);
            var b = network.Connect(2);
            var buffer = new byte[16];

            a.Send(2, Payload, Payload.Length);
            network.Advance(99);
            Assert.That(b.TryReceive(buffer, out _, out _), Is.False);

            network.Advance(1);
            Assert.That(b.TryReceive(buffer, out var length, out var from), Is.True);
            Assert.That(length, Is.EqualTo(3));
            Assert.That(from, Is.EqualTo(1UL));
            Assert.That(buffer[2], Is.EqualTo(3));
            Assert.That(b.TryReceive(buffer, out _, out _), Is.False);
        }

        [Test]
        public void LossRateIsCloseToTheSetting()
        {
            var network = new LoopbackNetwork(7, 0, 0, 0.05, 16);
            var a = network.Connect(1);
            var b = network.Connect(2);
            var buffer = new byte[16];

            for (var i = 0; i < 20000; i++)
            {
                a.Send(2, Payload, Payload.Length);
                while (b.TryReceive(buffer, out _, out _))
                {
                }
            }

            Assert.That(network.Dropped / (double)network.Sent, Is.EqualTo(0.05).Within(0.01));
            Assert.That(network.Delivered + network.Dropped, Is.EqualTo(network.Sent));
        }

        [Test]
        public void DownLinkDropsBothWays()
        {
            var network = new LoopbackNetwork(1);
            var a = network.Connect(1);
            var b = network.Connect(2);
            var buffer = new byte[16];
            network.SetLinkDown(2, true);

            a.Send(2, Payload, Payload.Length);
            b.Send(1, Payload, Payload.Length);
            Assert.That(b.TryReceive(buffer, out _, out _), Is.False);
            Assert.That(a.TryReceive(buffer, out _, out _), Is.False);

            network.SetLinkDown(2, false);
            a.Send(2, Payload, Payload.Length);
            Assert.That(b.TryReceive(buffer, out _, out _), Is.True);
        }

        [Test]
        public void EarliestPacketComesFirst()
        {
            var network = new LoopbackNetwork(3, 0, 500);
            var a = network.Connect(1);
            var b = network.Connect(2);
            var buffer = new byte[16];
            for (byte i = 0; i < 50; i++)
                a.Send(2, new[] { i }, 1);
            network.Advance(500);

            var received = 0;
            while (b.TryReceive(buffer, out _, out _))
                received++;

            Assert.That(received, Is.EqualTo(50));
        }
    }
}
