using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Telemetry;
using NUnit.Framework;

namespace Game.Core.Tests.Telemetry
{
    public class TelemetryServiceTests
    {
        private const long FirstDay = 20000;
        private const string Version = "1.0";

        private string _directory = string.Empty;
        private TelemetryStore _store = null!;

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ek-telemetry-tests-" + Guid.NewGuid().ToString("N"));
            _store = new TelemetryStore(_directory);
        }

        [TearDown]
        public void RemoveDirectory()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private TelemetryService Service(ITelemetrySink? sink = null) => new TelemetryService(_store, sink ?? NoTelemetrySink.Instance, Version);

        private static TelemetryProgress Progress(int prestiges = 0, double playSeconds = 0, long record = 12) =>
            new TelemetryProgress(record, prestiges, "viking", playSeconds);

        private static InputStats Session(double seconds = 0, ulong keys = 0, ulong leftClicks = 0, double meters = 0) =>
            new InputStats { PlaySeconds = seconds, Keys = keys, LeftClicks = leftClicks, MouseMeters = meters };

        [Test]
        public void WithoutConsentNothingIsCollectedOrWritten()
        {
            var service = Service();

            service.UseConsent(false, FirstDay, Session(), Progress());
            service.Observe(FirstDay, Session(60, 100), Progress());

            Assert.That(service.Enabled, Is.False);
            Assert.That(service.EndSession(Session(60, 100), Progress()), Is.Null);
            Assert.That(Directory.Exists(_store.Folder), Is.False);
        }

        [Test]
        public void StartingWithoutConsentClearsWhatAnEarlierConsentLeft()
        {
            var earlier = Service();
            earlier.UseConsent(true, FirstDay, Session(), Progress());
            earlier.Observe(FirstDay, Session(60, 100), Progress());

            Service().UseConsent(false, FirstDay + 1, Session(), Progress());

            Assert.That(Directory.Exists(_store.Folder), Is.False);
        }

        [Test]
        public void ConsentCreatesARandomAnonymousInstallId()
        {
            var first = Service();
            first.UseConsent(true, FirstDay, Session(), Progress());

            Assert.That(TelemetryJson.IsInstallId(first.State!.InstallId), Is.True);
            Assert.That(first.State.Sessions, Is.EqualTo(1));
            Assert.That(first.State.ReturnDays, Is.EqualTo(new[] { 0 }));
            Assert.That(File.Exists(_store.StatePath), Is.True);
            Assert.That(TelemetryService.NewInstallId(), Is.Not.EqualTo(TelemetryService.NewInstallId()));
        }

        [Test]
        public void SessionPacketHoldsRatesFromTheMomentOfConsent()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(100, 50, 5, 1), Progress(playSeconds: 1000));

            var packet = service.EndSession(Session(220, 290, 25, 13.46), Progress(playSeconds: 1120, record: 40))!;

            Assert.That(packet.SessionSeconds, Is.EqualTo(120));
            Assert.That(packet.KeysPerMinute, Is.EqualTo(120.0));
            Assert.That(packet.ClicksPerMinute, Is.EqualTo(10.0));
            Assert.That(packet.MouseMeters, Is.EqualTo(12.5));
            Assert.That(packet.TotalPlaySeconds, Is.EqualTo(1120));
            Assert.That(packet.RecordStage, Is.EqualTo(40));
            Assert.That(packet.Character, Is.EqualTo("viking"));
            Assert.That(packet.Closed, Is.True);
            Assert.That(packet.GameVersion, Is.EqualTo(Version));
            Assert.That(_store.QueuedFiles().Count, Is.EqualTo(1));
        }

        [Test]
        public void PacketCarriesOnlyAggregatesAndTheInstallId()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(), Progress());
            service.EndSession(Session(60, 120), Progress());

            var root = ContentNode.Root(File.ReadAllText(_store.QueuedFiles()[0]), "packet");

            Assert.That(root.FieldNames(), Is.EquivalentTo(new[]
            {
                "schema", "installId", "game", "session", "closed", "sessionSeconds", "keysPerMinute", "clicksPerMinute",
                "mouseMeters", "totalPlaySeconds", "firstPrestigeSeconds", "prestiges", "recordStage", "character",
                "daysSinceFirst", "returnDays"
            }));
            Assert.That(root.Field("installId").AsString(), Is.EqualTo(service.State!.InstallId));
        }

        [Test]
        public void TimeToFirstPrestigeIsTakenWhenThePrestigeIsSeen()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(), Progress(0, 100));

            service.Observe(FirstDay, Session(60), Progress(0, 160));
            service.Observe(FirstDay, Session(400), Progress(1, 500.4));
            service.Observe(FirstDay, Session(900), Progress(2, 1000));

            Assert.That(service.EndSession(Session(900), Progress(2, 1000))!.FirstPrestigeSeconds, Is.EqualTo(500));
        }

        [Test]
        public void PrestigeBeforeConsentLeavesTheTimeUnknown()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(), Progress(3, 9000));

            Assert.That(service.EndSession(Session(60), Progress(3, 9060))!.FirstPrestigeSeconds, Is.Null);
        }

        [Test]
        public void ReturnDaysSurviveRestartsWithTheSameId()
        {
            var first = Service();
            first.UseConsent(true, FirstDay, Session(), Progress());
            first.EndSession(Session(60), Progress());
            var id = first.State!.InstallId;

            foreach (var day in new[] { FirstDay + 1, FirstDay + 1, FirstDay + 7 })
            {
                var next = Service();
                next.UseConsent(true, day, Session(), Progress());
                next.EndSession(Session(60), Progress());
                Assert.That(next.State!.InstallId, Is.EqualTo(id));
            }

            var last = TelemetryJson.ReadPacket(File.ReadAllText(_store.QueuedFiles().Last()), "packet");
            Assert.That(last.ReturnDays, Is.EqualTo(new[] { 0, 1, 7 }));
            Assert.That(last.DaysSinceFirst, Is.EqualTo(7));
            Assert.That(last.Session, Is.EqualTo(4));
        }

        [Test]
        public void NewLocalDayClosesTheSessionAndStartsAnother()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(), Progress());

            service.Observe(FirstDay, Session(3600, 600), Progress());
            service.Observe(FirstDay + 1, Session(7200, 1800), Progress());

            Assert.That(_store.QueuedFiles().Count, Is.EqualTo(1));
            var closed = TelemetryJson.ReadPacket(File.ReadAllText(_store.QueuedFiles()[0]), "packet");
            Assert.That(closed.Closed, Is.True);
            Assert.That(closed.SessionSeconds, Is.EqualTo(7200));
            var next = service.EndSession(Session(9000, 1800), Progress())!;
            Assert.That(next.Session, Is.EqualTo(2));
            Assert.That(next.SessionSeconds, Is.EqualTo(1800));
            Assert.That(next.DaysSinceFirst, Is.EqualTo(1));
        }

        [Test]
        public void UnfinishedSessionIsQueuedOnTheNextStart()
        {
            var crashed = Service();
            crashed.UseConsent(true, FirstDay, Session(), Progress());
            crashed.Observe(FirstDay, Session(600, 1200), Progress());

            Service().UseConsent(true, FirstDay + 1, Session(), Progress());

            var packet = TelemetryJson.ReadPacket(File.ReadAllText(_store.QueuedFiles().Single()), "packet");
            Assert.That(packet.Closed, Is.False);
            Assert.That(packet.SessionSeconds, Is.EqualTo(600));
            Assert.That(packet.KeysPerMinute, Is.EqualTo(120.0));
        }

        [Test]
        public void WithdrawingConsentForgetsTheIdAndTheQueue()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(), Progress());
            service.EndSession(Session(60), Progress());
            var id = service.State!.InstallId;

            service.UseConsent(false, FirstDay, Session(), Progress());

            Assert.That(service.Enabled, Is.False);
            Assert.That(Directory.Exists(_store.Folder), Is.False);
            service.UseConsent(true, FirstDay, Session(), Progress());
            Assert.That(service.State!.InstallId, Is.Not.EqualTo(id));
        }

        [Test]
        public void FlushSendsInOrderAndKeepsWhatTheSinkRefused()
        {
            var sink = new CountingSink(2);
            var service = Service(sink);
            service.UseConsent(true, FirstDay, Session(), Progress());
            for (var i = 1; i <= 3; i++)
                service.EndSession(Session(60 * i), Progress());

            Assert.That(service.Flush(), Is.EqualTo(2));
            Assert.That(_store.QueuedFiles().Count, Is.EqualTo(1));
            Assert.That(sink.Sent.Select(text => TelemetryJson.ReadPacket(text, "packet").SessionSeconds), Is.EqualTo(new[] { 60L, 120L }));
        }

        [Test]
        public void WithoutASinkPacketsStayOnDisk()
        {
            var service = Service();
            service.UseConsent(true, FirstDay, Session(), Progress());
            service.EndSession(Session(60), Progress());

            Assert.That(service.Flush(), Is.EqualTo(0));
            Assert.That(_store.QueuedFiles().Count, Is.EqualTo(1));
        }

        [Test]
        public void QueueKeepsOnlyTheNewestPackets()
        {
            for (var i = 1; i <= TelemetryStore.MaxQueued + 5; i++)
                _store.Enqueue(new TelemetryPacket { InstallId = TelemetryService.NewInstallId(), GameVersion = Version, Character = "viking", Session = i });

            var queued = _store.QueuedFiles();

            Assert.That(queued.Count, Is.EqualTo(TelemetryStore.MaxQueued));
            Assert.That(TelemetryJson.ReadPacket(File.ReadAllText(queued[0]), "packet").Session, Is.EqualTo(6));
            Assert.That(TelemetryJson.ReadPacket(File.ReadAllText(queued[queued.Count - 1]), "packet").Session, Is.EqualTo(TelemetryStore.MaxQueued + 5));
        }

        [Test]
        public void DamagedStateStartsOverWithAProblem()
        {
            Directory.CreateDirectory(_store.Folder);
            File.WriteAllText(_store.StatePath, "{ \"version\": 1, \"installId\": \"C:\\\\Users\\\\me\" }");
            var problems = new List<string>();
            var service = Service();
            service.Problem += problems.Add;

            service.UseConsent(true, FirstDay, Session(), Progress());

            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(TelemetryJson.IsInstallId(service.State!.InstallId), Is.True);
        }

        private sealed class CountingSink : ITelemetrySink
        {
            private readonly int _accepts;

            public CountingSink(int accepts) => _accepts = accepts;

            public List<string> Sent { get; } = new List<string>();

            public bool TrySend(string packetJson)
            {
                if (Sent.Count >= _accepts)
                    return false;
                Sent.Add(packetJson);
                return true;
            }
        }
    }
}
