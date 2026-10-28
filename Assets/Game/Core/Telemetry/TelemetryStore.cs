using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Content;
using Game.Core.Save;

namespace Game.Core.Telemetry
{
    public sealed class TelemetryStore
    {
        public const string FolderName = "telemetry";
        public const string QueueFolderName = "queue";
        public const int MaxQueued = 100;

        private const string PacketPattern = "*.json";
        private const string PacketNumberFormat = "D8";

        public TelemetryStore(string directory)
        {
            Folder = Path.Combine(directory, FolderName);
        }

        public string Folder { get; }
        public string StatePath => Path.Combine(Folder, TelemetryJson.StateFileName);
        public string QueueFolder => Path.Combine(Folder, QueueFolderName);

        public TelemetryState? LoadState(List<string> problems)
        {
            if (!File.Exists(StatePath))
                return null;
            try
            {
                return TelemetryJson.ReadState(File.ReadAllText(StatePath, AtomicFile.Utf8));
            }
            catch (ContentException error)
            {
                problems.Add(error.Message);
                return null;
            }
        }

        public void SaveState(TelemetryState state) => AtomicFile.Write(StatePath, TelemetryJson.Write(state), StatePath + ".bak");

        public IReadOnlyList<string> QueuedFiles()
        {
            if (!Directory.Exists(QueueFolder))
                return Array.Empty<string>();
            var files = Directory.GetFiles(QueueFolder, PacketPattern);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public string Enqueue(TelemetryPacket packet)
        {
            Directory.CreateDirectory(QueueFolder);
            var queued = QueuedFiles();
            var path = Path.Combine(QueueFolder, NextNumber(queued).ToString(PacketNumberFormat, CultureInfo.InvariantCulture) + ".json");
            AtomicFile.WriteDurably(path, TelemetryJson.Write(packet));
            for (var i = 0; i <= queued.Count - MaxQueued; i++)
                File.Delete(queued[i]);
            return path;
        }

        public int Flush(ITelemetrySink sink)
        {
            var sent = 0;
            foreach (var path in QueuedFiles())
            {
                if (!sink.TrySend(File.ReadAllText(path, AtomicFile.Utf8)))
                    break;
                File.Delete(path);
                sent++;
            }
            return sent;
        }

        public void Forget()
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }

        private static long NextNumber(IReadOnlyList<string> queued)
        {
            if (queued.Count == 0)
                return 1;
            var last = Path.GetFileNameWithoutExtension(queued[queued.Count - 1]);
            return long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number + 1 : queued.Count + 1;
        }
    }
}
