using System;
using System.Collections.Concurrent;
using System.IO;
using Game.Core.Diagnostics;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class ErrorReportDriver : MonoBehaviour
    {
        private const string PreviousLogName = "Player-prev.log";
        private const float FlushSeconds = 60f;

        private readonly ConcurrentQueue<string> _problems = new ConcurrentQueue<string>();
        private ErrorReporter _reporter = null!;
        private GameSettings _settings = null!;
        private float _sinceFlush;

        public static ErrorReportDriver Attach(GameObject host, GameSettings settings)
        {
            var driver = host.AddComponent<ErrorReportDriver>();
            driver._settings = settings;
            var scrubber = new LogScrubber(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName);
            driver._reporter = new ErrorReporter(new ErrorReportStore(SaveDriver.SaveDirectory), scrubber, Application.version, SystemInfo.operatingSystem, () => DateTimeOffset.UtcNow);
            driver._reporter.Problem += driver._problems.Enqueue;
            driver._reporter.StartSession(ReadPreviousLog());
            Application.logMessageReceivedThreaded += driver.OnLog;
            return driver;
        }

        private static string? ReadPreviousLog()
        {
            try
            {
                var folder = Path.GetDirectoryName(Application.consoleLogPath);
                if (string.IsNullOrEmpty(folder))
                    return null;
                var path = Path.Combine(folder, PreviousLogName);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static LogLevel LevelOf(LogType type) => type switch
        {
            LogType.Exception => LogLevel.Exception,
            LogType.Error or LogType.Assert => LogLevel.Error,
            LogType.Warning => LogLevel.Warning,
            _ => LogLevel.Info
        };

        private void OnLog(string message, string stackTrace, LogType type) => _reporter.OnLog(LevelOf(type), message, stackTrace);

        private void Update()
        {
            while (_problems.TryDequeue(out var problem))
                Debug.LogWarning("Error reports: " + problem);
            _sinceFlush += Time.unscaledDeltaTime;
            if (_sinceFlush < FlushSeconds)
                return;
            _sinceFlush = 0f;
            _reporter.Flush(NoErrorReportSink.Instance, _settings.ErrorReportConsentGiven);
        }

        private void OnApplicationQuit()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            _reporter.Flush(NoErrorReportSink.Instance, _settings.ErrorReportConsentGiven);
            _reporter.StopSession();
        }
    }
}
