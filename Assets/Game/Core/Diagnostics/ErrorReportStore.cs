using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Save;

namespace Game.Core.Diagnostics
{
    public sealed class ErrorReportStore
    {
        public const string FolderName = "reports";
        public const string RunningFileName = "running.marker";
        public const int MaxReports = 20;

        private const string ReportPattern = "*.json";
        private const string ReportNumberFormat = "D6";

        public ErrorReportStore(string directory)
        {
            Folder = Path.Combine(directory, FolderName);
        }

        public string Folder { get; }
        public string RunningPath => Path.Combine(Folder, RunningFileName);

        public IReadOnlyList<string> Reports()
        {
            if (!Directory.Exists(Folder))
                return Array.Empty<string>();
            var files = Directory.GetFiles(Folder, ReportPattern);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public string Write(ErrorReport report)
        {
            Directory.CreateDirectory(Folder);
            var existing = Reports();
            var name = NextNumber(existing).ToString(ReportNumberFormat, CultureInfo.InvariantCulture) + "-" + report.Kind + ".json";
            var path = Path.Combine(Folder, name);
            AtomicFile.WriteDurably(path, ErrorReportJson.Write(report));
            for (var i = 0; i <= existing.Count - MaxReports; i++)
                File.Delete(existing[i]);
            return path;
        }

        public int Flush(IErrorReportSink sink)
        {
            var sent = 0;
            foreach (var path in Reports())
            {
                if (!sink.TrySend(File.ReadAllText(path, AtomicFile.Utf8)))
                    break;
                File.Delete(path);
                sent++;
            }
            return sent;
        }

        public bool MarkRunning(string gameVersion)
        {
            Directory.CreateDirectory(Folder);
            var crashed = File.Exists(RunningPath);
            AtomicFile.WriteDurably(RunningPath, gameVersion + "\n");
            return crashed;
        }

        public void MarkStopped()
        {
            if (File.Exists(RunningPath))
                File.Delete(RunningPath);
        }

        private static long NextNumber(IReadOnlyList<string> existing)
        {
            if (existing.Count == 0)
                return 1;
            var last = Path.GetFileNameWithoutExtension(existing[existing.Count - 1]);
            var dash = last.IndexOf('-');
            var digits = dash > 0 ? last.Substring(0, dash) : last;
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number + 1 : existing.Count + 1;
        }
    }
}
