using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core.Content;

namespace Game.Core.Save
{
    public sealed class SaveStore
    {
        public const int BackupCount = 3;
        public const double DefaultBackupRotationSeconds = 600;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly double _backupRotationSeconds;

        public SaveStore(string directory, double backupRotationSeconds = DefaultBackupRotationSeconds)
        {
            Directory = directory;
            _backupRotationSeconds = backupRotationSeconds;
        }

        public string Directory { get; }
        public string MainPath => Path.Combine(Directory, SaveCodec.FileName);
        public string TempPath => MainPath + ".tmp";

        public string BackupPath(int number) => MainPath + ".bak" + number;

        public void Write(string text)
        {
            System.IO.Directory.CreateDirectory(Directory);
            WriteDurably(TempPath, text);
            if (!File.Exists(MainPath))
            {
                File.Move(TempPath, MainPath);
                return;
            }
            if (ShouldRotateBackups())
                RotateBackups();
            File.Replace(TempPath, MainPath, BackupPath(1));
        }

        public SaveLoadResult Load(Func<string, SaveFile> decode)
        {
            var problems = new List<string>();
            SaveFile? best = null;
            string? bestPath = null;
            foreach (var path in Candidates())
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    var save = decode(File.ReadAllText(path, Utf8));
                    if (best == null || save.State.Tick > best.State.Tick)
                    {
                        best = save;
                        bestPath = path;
                    }
                }
                catch (ContentException error)
                {
                    problems.Add(Path.GetFileName(path) + ": " + error.Message);
                }
                catch (IOException error)
                {
                    problems.Add(Path.GetFileName(path) + ": " + error.Message);
                }
            }
            return new SaveLoadResult(best, bestPath, problems);
        }

        private IEnumerable<string> Candidates()
        {
            yield return MainPath;
            yield return TempPath;
            for (var number = 1; number <= BackupCount; number++)
                yield return BackupPath(number);
        }

        private bool ShouldRotateBackups()
        {
            var second = BackupPath(2);
            if (!File.Exists(second))
                return true;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(second);
            return age.TotalSeconds >= _backupRotationSeconds || age.TotalSeconds < 0;
        }

        private void RotateBackups()
        {
            for (var number = BackupCount; number > 1; number--)
            {
                var older = BackupPath(number);
                var newer = BackupPath(number - 1);
                if (!File.Exists(newer))
                    continue;
                if (File.Exists(older))
                    File.Delete(older);
                File.Move(newer, older);
            }
        }

        private static void WriteDurably(string path, string text)
        {
            var bytes = Utf8.GetBytes(text);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
    }
}
