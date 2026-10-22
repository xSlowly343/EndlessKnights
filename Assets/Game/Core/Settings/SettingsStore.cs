using System.Collections.Generic;
using System.IO;
using Game.Core.Save;

namespace Game.Core.Settings
{
    public sealed class SettingsStore
    {
        public SettingsStore(string directory)
        {
            Directory = directory;
        }

        public string Directory { get; }
        public string MainPath => Path.Combine(Directory, SettingsCodec.FileName);
        public string BackupPath => MainPath + ".bak";

        public void Write(GameSettings settings) => AtomicFile.Write(MainPath, SettingsCodec.Encode(settings), BackupPath);

        public GameSettings Load(List<string> problems)
        {
            foreach (var path in new[] { MainPath, BackupPath })
            {
                if (!File.Exists(path))
                    continue;
                var fileProblems = new List<string>();
                var settings = SettingsCodec.Decode(File.ReadAllText(path, AtomicFile.Utf8), fileProblems);
                problems.AddRange(fileProblems);
                if (fileProblems.Count == 0 || path == BackupPath || !File.Exists(BackupPath))
                    return settings;
            }
            return new GameSettings();
        }
    }
}
