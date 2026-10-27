using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Diagnostics;
using NUnit.Framework;

namespace Game.Core.Tests.Diagnostics
{
    public class ErrorReporterTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 27, 14, 5, 9, TimeSpan.FromHours(3));

        private string _directory = string.Empty;
        private ErrorReportStore _store = null!;

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ek-reports-tests-" + Guid.NewGuid().ToString("N"));
            _store = new ErrorReportStore(_directory);
        }

        [TearDown]
        public void RemoveDirectory()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private ErrorReporter Reporter() =>
            new ErrorReporter(_store, new LogScrubber(@"C:\Users\Ivan", "Ivan"), "1.0", "Windows 11 (10.0.22631) 64bit", () => Now);

        [Test]
        public void RingKeepsTheLastLinesInOrder()
        {
            var ring = new LogRing(3);
            foreach (var line in new[] { "a", "b", "c", "d", "e" })
                ring.Add(line);

            Assert.That(ring.Count, Is.EqualTo(3));
            Assert.That(ring.Last(10), Is.EqualTo(new[] { "c", "d", "e" }));
            Assert.That(ring.Last(2), Is.EqualTo(new[] { "d", "e" }));
            Assert.That(ring.Last(0), Is.Empty);
        }

        [Test]
        public void RingTakesLinesFromManyThreads()
        {
            var ring = new LogRing(50);

            Parallel.For(0, 1000, i => ring.Add("line " + i));

            Assert.That(ring.Count, Is.EqualTo(50));
            Assert.That(ring.Last(50), Has.All.StartWith("line "));
        }

        [Test]
        public void InfoAndWarningsOnlyGoToTheRing()
        {
            var reporter = Reporter();

            Assert.That(reporter.OnLog(LogLevel.Info, "Save: loaded", string.Empty), Is.Null);
            Assert.That(reporter.OnLog(LogLevel.Warning, "Settings: odd value", string.Empty), Is.Null);

            Assert.That(reporter.Ring.Last(10), Is.EqualTo(new[] { "11:05:09 Info Save: loaded", "11:05:09 Warning Settings: odd value" }));
            Assert.That(_store.Reports(), Is.Empty);
        }

        [Test]
        public void ExceptionWritesAReportWithVersionOsTimeLinesAndStack()
        {
            var reporter = Reporter();
            reporter.OnLog(LogLevel.Info, @"Save: loaded from C:\Users\Ivan\AppData\Roaming\RiftWarden", string.Empty);

            var report = reporter.OnLog(LogLevel.Exception, "NullReferenceException: Object reference not set",
                "Game.Bootstrap.BattleDriver.Update () (at C:/Users/Ivan/src/BattleDriver.cs:42)\nUnityEngine.Behaviour.Update ()\n")!;

            Assert.That(report.Kind, Is.EqualTo(ErrorReport.ErrorKind));
            Assert.That(report.GameVersion, Is.EqualTo("1.0"));
            Assert.That(report.Os, Is.EqualTo("Windows 11 (10.0.22631) 64bit"));
            Assert.That(report.CreatedUtc, Is.EqualTo("2026-10-27T11:05:09Z"));
            Assert.That(report.StackTrace, Is.EqualTo("Game.Bootstrap.BattleDriver.Update () (at %USER%/src/BattleDriver.cs:42)\nUnityEngine.Behaviour.Update ()"));
            Assert.That(report.Lines.Count, Is.EqualTo(2));
            Assert.That(report.Lines[0], Is.EqualTo(@"11:05:09 Info Save: loaded from %USER%\AppData\Roaming\RiftWarden"));
            var saved = ErrorReportJson.Read(File.ReadAllText(_store.Reports().Single()), "report");
            Assert.That(ErrorReportJson.Write(saved), Is.EqualTo(ErrorReportJson.Write(report)));
        }

        [Test]
        public void SavedReportNeverMentionsTheUser()
        {
            var reporter = Reporter();
            reporter.OnLog(LogLevel.Info, "Steam name Ivan", string.Empty);
            reporter.OnLog(LogLevel.Error, @"Save failed: C:\Users\Ivan\save.json", @"at C:\Users\Ivan\x.cs");

            var text = File.ReadAllText(_store.Reports().Single());

            Assert.That(text, Does.Not.Contain("Ivan"));
            Assert.That(text, Does.Contain("%USER%"));
        }

        [Test]
        public void SameErrorIsReportedOncePerSession()
        {
            var reporter = Reporter();

            reporter.OnLog(LogLevel.Exception, "Boom", "at A.B ()\nat C.D ()");
            reporter.OnLog(LogLevel.Exception, "Boom", "at A.B ()\nat E.F ()");

            Assert.That(_store.Reports().Count, Is.EqualTo(1));
            Assert.That(reporter.Ring.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReportsPerSessionAreCapped()
        {
            var reporter = Reporter();

            for (var i = 0; i < 20; i++)
                reporter.OnLog(LogLevel.Error, "Error " + i, string.Empty);

            Assert.That(_store.Reports().Count, Is.EqualTo(ErrorReporter.MaxReportsPerSession));
            Assert.That(reporter.ReportsThisSession, Is.EqualTo(ErrorReporter.MaxReportsPerSession));
        }

        [Test]
        public void FolderKeepsOnlyTheNewestReports()
        {
            for (var i = 1; i <= ErrorReportStore.MaxReports + 3; i++)
                _store.Write(new ErrorReport { Message = "report " + i, CreatedUtc = "2026-10-27T00:00:00Z" });

            var reports = _store.Reports();

            Assert.That(reports.Count, Is.EqualTo(ErrorReportStore.MaxReports));
            Assert.That(ErrorReportJson.Read(File.ReadAllText(reports[0]), "report").Message, Is.EqualTo("report 4"));
        }

        [Test]
        public void CleanExitLeavesNoCrashReport()
        {
            var first = Reporter();
            Assert.That(first.StartSession("old log"), Is.Null);
            first.StopSession();

            Assert.That(Reporter().StartSession("old log"), Is.Null);
            Assert.That(_store.Reports(), Is.Empty);
        }

        [Test]
        public void SessionWithoutCleanExitBecomesACrashReportFromThePreviousLog()
        {
            Reporter().StartSession(null);
            var previousLog = string.Join("\r\n", Enumerable.Range(1, 150).Select(i => "line " + i)) + "\r\nCrash!!! at C:\\Users\\Ivan\\game.dll\r\n\r\n";

            var crash = Reporter().StartSession(previousLog)!;

            Assert.That(crash.Kind, Is.EqualTo(ErrorReport.CrashKind));
            Assert.That(crash.Message, Is.EqualTo(ErrorReporter.CrashMessage));
            Assert.That(crash.Lines.Count, Is.EqualTo(ErrorReporter.LinesPerReport));
            Assert.That(crash.Lines.Last(), Is.EqualTo(@"Crash!!! at %USER%\game.dll"));
            Assert.That(crash.Lines.First(), Is.EqualTo("line 52"));
            Assert.That(_store.Reports().Single(), Does.EndWith("-crash.json"));
        }

        [Test]
        public void ReportsAreSentOnlyWithConsent()
        {
            var reporter = Reporter();
            reporter.OnLog(LogLevel.Error, "one", string.Empty);
            reporter.OnLog(LogLevel.Error, "two", string.Empty);
            var sink = new CollectingSink();

            Assert.That(reporter.Flush(sink, false), Is.EqualTo(0));
            Assert.That(sink.Sent, Is.Empty);
            Assert.That(reporter.Flush(NoErrorReportSink.Instance, true), Is.EqualTo(0));
            Assert.That(reporter.Flush(sink, true), Is.EqualTo(2));
            Assert.That(_store.Reports(), Is.Empty);
            Assert.That(ErrorReportJson.Read(sink.Sent[0], "report").Message, Is.EqualTo("one"));
        }

        [Test]
        public void TailSkipsTrailingBlankLines()
        {
            Assert.That(LogTail.Last("a\nb\n\n  \n", 5), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(LogTail.Last(null, 5), Is.Empty);
        }

        private sealed class CollectingSink : IErrorReportSink
        {
            public List<string> Sent { get; } = new List<string>();

            public bool TrySend(string reportJson)
            {
                Sent.Add(reportJson);
                return true;
            }
        }
    }
}
