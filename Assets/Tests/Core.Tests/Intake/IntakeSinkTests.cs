using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Intake;
using NUnit.Framework;

namespace Game.Core.Tests.Intake
{
    public class IntakeSinkTests
    {
        private sealed class FakePost : IIntakePost
        {
            public readonly List<(string Url, string Json)> Calls = new List<(string, string)>();
            public IntakeOutcome Next = IntakeOutcome.Delivered;
            public bool Throw;

            public IntakeOutcome Post(string url, string json)
            {
                Calls.Add((url, json));
                if (Throw)
                    throw new InvalidOperationException("no network");
                return Next;
            }
        }

        private sealed class Later
        {
            public readonly Queue<Action> Work = new Queue<Action>();

            public void Run(Action work) => Work.Enqueue(work);

            public void Finish()
            {
                while (Work.Count > 0)
                    Work.Dequeue()();
            }
        }

        [Test]
        public void ADeliveredPacketIsConfirmedAtOnceWhenTheSendIsInline()
        {
            var post = new FakePost();
            var sink = new IntakeSink(post, "https://game.example/intake/telemetry", work => work());

            Assert.That(sink.TrySend("{\"a\":1}"), Is.True);
            Assert.That(post.Calls, Is.EqualTo(new[] { ("https://game.example/intake/telemetry", "{\"a\":1}") }));
        }

        [Test]
        public void TheGameIsNeverBlockedAndThePacketIsConfirmedOnTheNextFlush()
        {
            var post = new FakePost();
            var later = new Later();
            var sink = new IntakeSink(post, "u", later.Run);

            Assert.That(sink.TrySend("p1"), Is.False, "the send runs in the background");
            Assert.That(sink.TrySend("p2"), Is.False, "one packet at a time");
            later.Finish();

            Assert.That(post.Calls.Count, Is.EqualTo(1));
            Assert.That(sink.TrySend("p1"), Is.True);
            Assert.That(sink.Delivered, Is.EqualTo(1));
        }

        [Test]
        public void ARefusedPacketIsDroppedSoTheQueueDoesNotStick()
        {
            var post = new FakePost { Next = IntakeOutcome.Rejected };
            var sink = new IntakeSink(post, "u", work => work());

            Assert.That(sink.TrySend("bad"), Is.True);
            Assert.That(sink.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void AFailedSendIsKeptAndTriedAgain()
        {
            var post = new FakePost { Throw = true };
            var sink = new IntakeSink(post, "u", work => work());

            Assert.That(sink.TrySend("p"), Is.False);
            Assert.That(sink.Failed, Is.EqualTo(1));
            post.Throw = false;
            Assert.That(sink.TrySend("p"), Is.True);
            Assert.That(post.Calls.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheShippedNetworkFileHasNoServerYet()
        {
            var config = NetworkConfig.Load(System.IO.File.ReadAllText(Game.Core.Tests.Party.PartyVectorTests.RepoFile(System.IO.Path.Combine("Assets", "StreamingAssets", NetworkConfig.FileName))));

            Assert.That(config.HasServer, Is.False);
        }

        [Test]
        public void TheServerUrlGivesBothIntakeAddresses()
        {
            var config = NetworkConfig.Load("{ \"version\": 1, \"serverUrl\": \"https://play.example.com/\", \"serverKey\": null }");

            Assert.That(config.HasParty, Is.False, "the party needs the server key too");
            Assert.That(NetworkConfig.Load("{ \"version\": 1, \"serverUrl\": \"https://play.example.com\", \"serverKey\": \"k1\" }").HasParty, Is.True);
            Assert.That(config.TelemetryUrl, Is.EqualTo("https://play.example.com/intake/telemetry"));
            Assert.That(config.ErrorsUrl, Is.EqualTo("https://play.example.com/intake/errors"));
        }

        [Test]
        public void APlainHttpServerIsRefused()
        {
            var error = Assert.Throws<ContentException>(() => NetworkConfig.Load("{ \"version\": 1, \"serverUrl\": \"http://play.example.com\", \"serverKey\": null }"));

            Assert.That(error!.Message, Does.Contain("https://"));
        }
    }
}
