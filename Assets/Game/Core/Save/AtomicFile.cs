using System.IO;
using System.Text;

namespace Game.Core.Save
{
    public static class AtomicFile
    {
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static void Write(string path, string text, string backupPath)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var temp = path + ".tmp";
            WriteDurably(temp, text);
            if (File.Exists(path))
                File.Replace(temp, path, backupPath);
            else
                File.Move(temp, path);
        }

        public static void WriteDurably(string path, string text)
        {
            var bytes = Utf8.GetBytes(text);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
    }
}
