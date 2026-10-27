using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Game.Core.Diagnostics
{
    public sealed class ErrorReporter
    {
        public const int RingCapacity = 200;
        public const int LinesPerReport = 100;
        public const int MaxReportsPerSession = 5;
        public const string CrashMessage = "The previous session ended without a clean exit";

        private const string LineTimeFormat = "HH:mm:ss";
        private const string CreatedFormat = "yyyy-MM-ddTHH:mm:ssZ";

        private readonly object _gate = new object();
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly ErrorReportStore _store;
        private readonly LogScrubber _scrubber;
        private readonly string _gameVersion;
        private readonly string _os;
        private readonly Func<DateTimeOffset> _clock;
        private int _written;
        private bool _writing;

        public ErrorReporter(ErrorReportStore store, LogScrubber scrubber, string gameVersion, string os, Func<DateTimeOffset> clock)
        {
            _store = store;
            _scrubber = scrubber;
            _gameVersion = gameVersion;
            _os = os;
            _clock = clock;
        }

        public event Action<string>? Problem;

        public LogRing Ring { get; } = new LogRing(RingCapacity);

        public int ReportsThisSession
        {
            get
            {
                lock (_gate)
                    return _written;
            }
        }

        public ErrorReport? OnLog(LogLevel level, string message, string stackTrace)
        {
            var now = _clock().ToUniversalTime();
            Ring.Add(_scrubber.Scrub(now.ToString(LineTimeFormat, CultureInfo.InvariantCulture) + " " + level + " " + message));
            if (level < LogLevel.Error || !TryClaimReport(level + "\n" + message + "\n" + FirstLine(stackTrace)))
                return null;
            try
            {
                var report = Build(ErrorReport.ErrorKind, message, stackTrace, Ring.Last(LinesPerReport), now);
                Guard(() => _store.Write(report));
                return report;
            }
            finally
            {
                lock (_gate)
                    _writing = false;
            }
        }

        public ErrorReport? StartSession(string? previousLog)
        {
            var crashed = false;
            Guard(() => crashed = _store.MarkRunning(_gameVersion));
            if (!crashed)
                return null;
            var report = Build(ErrorReport.CrashKind, CrashMessage, string.Empty, LogTail.Last(previousLog, LinesPerReport), _clock().ToUniversalTime());
            Guard(() => _store.Write(report));
            return report;
        }

        public void StopSession() => Guard(_store.MarkStopped);

        public int Flush(IErrorReportSink sink, bool consentGiven)
        {
            if (!consentGiven)
                return 0;
            var sent = 0;
            Guard(() => sent = _store.Flush(sink));
            return sent;
        }

        private bool TryClaimReport(string key)
        {
            lock (_gate)
            {
                if (_writing || _written >= MaxReportsPerSession || !_reported.Add(key))
                    return false;
                _writing = true;
                _written++;
                return true;
            }
        }

        private ErrorReport Build(string kind, string message, string stackTrace, IReadOnlyList<string> lines, DateTimeOffset now)
        {
            var scrubbed = new string[lines.Count];
            for (var i = 0; i < scrubbed.Length; i++)
                scrubbed[i] = _scrubber.Scrub(lines[i]);
            return new ErrorReport
            {
                Kind = kind,
                GameVersion = _gameVersion,
                Os = _scrubber.Scrub(_os),
                CreatedUtc = now.ToString(CreatedFormat, CultureInfo.InvariantCulture),
                Message = _scrubber.Scrub(message),
                StackTrace = ScrubLines(stackTrace),
                Lines = scrubbed
            };
        }

        private string ScrubLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                lines[i] = _scrubber.Scrub(lines[i].TrimEnd('\r'));
            return string.Join("\n", lines).TrimEnd('\n');
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var end = text.IndexOf('\n');
            return end < 0 ? text : text.Substring(0, end);
        }

        private void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (IOException error)
            {
                Problem?.Invoke(error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Problem?.Invoke(error.Message);
            }
        }
    }
}
